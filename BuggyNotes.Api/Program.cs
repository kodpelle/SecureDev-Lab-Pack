using Microsoft.EntityFrameworkCore;
using BuggyNotes.Api.Data;
using BuggyNotes.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BuggyNotes.Api.Auth;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;


var builder = WebApplication.CreateBuilder(args);

var jwtOptions = new JwtOptions();

builder.Services
  .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(o =>
  {
      o.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidateAudience = true,
          ValidateLifetime = true,
          ValidateIssuerSigningKey = true,
          ValidIssuer = jwtOptions.Issuer,
          ValidAudience = jwtOptions.Audience,
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
      };
  });

builder.Services.AddDbContext<AppDb>(options =>
    options
        .UseSqlite(builder.Configuration.GetConnectionString("Default"))
        .LogTo(Console.WriteLine, LogLevel.Information) // prints SQL etc.
        .EnableSensitiveDataLogging() // DEV ONLY: visar parameter-värden i logg
);


builder.Services.AddAuthorization();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);


var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

var log = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("BuggyNotes.App");
log.LogInformation("App started"); 

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Seed (för att ha något att söka på första gången)
app.MapPost("/seed", async (AppDb db) =>
{
    log.LogInformation("POST /seed called");
    if (!db.Notes.Any())
    {
        db.Notes.AddRange(new[] {
            new Note { Title = "Hello", Content = "World" },
            new Note { Title = "Tips", Content = "<b>Bold?</b>" },
            new Note { Title = "SQL", Content = "LIKE and injection" }
        });
        var saved = await db.SaveChangesAsync();
        log.LogInformation("Seed inserted {Count} notes", saved);
    }
    return Results.Ok(new { seeded = true });
});

app.MapGet("/notes", async (AppDb db) =>
{
    log.LogInformation("GET /notes");
    var list = await db.Notes.ToListAsync();
    log.LogInformation("Returned {Count} notes", list.Count);
    return list;
});

app.MapGet("/notes/search-bug", async (AppDb db, string q) =>
{
    log.LogWarning("GET /notes/search-bug?q={Q}", q);
    var sql = $"SELECT * FROM Notes WHERE Title LIKE '%{q}%'";
    var list = await db.Notes.FromSqlRaw(sql).ToListAsync();
    log.LogWarning("BUGGY search returned {Count} notes", list.Count);
    return Results.Ok(list);
});

app.MapGet("/notes/search-safe", async (AppDb db, string q) =>
{
    log.LogInformation("GET /notes/search-safe?q={Q}", q);
    var list = await db.Notes
        .Where(n => EF.Functions.Like(n.Title, $"%{q}%"))
        .ToListAsync();
    log.LogInformation("SAFE search returned {Count} notes", list.Count);
    return Results.Ok(list);
});

app.MapPost("/auth/register", async (AppDb db, RegisterDto dto) =>
{
    var hasher = new PasswordHasher<User>();
    log.LogInformation("POST /auth/register called {User}", dto.UserName);

    if (await db.Users.AnyAsync(u => u.UserName == dto.UserName))
    {
        log.LogWarning("User already exists: {UserName}", dto.UserName);
        return Results.BadRequest(new { message = "User already exists" });
    }
    var user = new User { UserName = dto.UserName };

    user.PasswordHash = hasher.HashPassword(user, dto.Password);

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{user.Id}", new
    {
        user.Id,
        user.UserName,
        message = "User registered successfully"
    });
});
app.MapPost("/auth/login", async (AppDb db, LoginDto dto) =>
{
    log.LogInformation("POST /auth/login {User}", dto.UserName);

    var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName);
    if (user is null) return Results.Unauthorized();

    var hasher = new PasswordHasher<User>();
    var vr = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
    if (vr == PasswordVerificationResult.Failed) return Results.Unauthorized();

    var token = JwtIssuer.CreateToken(user.Id.ToString(), user.UserName, jwtOptions);
    return Results.Ok(new { token });
});

app.MapGet("/me", (ClaimsPrincipal user) =>
{
    Console.WriteLine("[DEBUG] /me hit");
    var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "?";
    var name = user.Identity?.Name ?? "?";
    return Results.Ok(new { id, name });
})
.RequireAuthorization();

app.MapGet("/__routes", (IEnumerable<EndpointDataSource> sources) =>
{
    var list = new List<string>();
    foreach (var s in sources)
        foreach (var e in s.Endpoints)
            list.Add(e.DisplayName ?? "(no name)");
    return Results.Ok(list);
});

app.Run();