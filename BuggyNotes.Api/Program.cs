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

var insecure = builder.Configuration.GetValue<bool>("Demo:InsecureMode");

var secret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(secret))
{
    secret = "THIS_IS_WEAK_AND_FOR_DEMO_ONLY_CHANGE_ME";
    Console.WriteLine("[WARN] Jwt:Secret missing – using DEV fallback.");
}

var jwtOptions = new JwtOptions
{
    Secret = secret!,
    Issuer = "BuggyNotes",
    Audience = "BuggyNotesAudience",
    ExpiryMinutes = 15
};

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
          IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
          ClockSkew = TimeSpan.FromSeconds(30)
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

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Demo-Mode"] = insecure ? "insecure" : "secure";
    await next();
});


app.UseAuthentication();
app.UseAuthorization();

var log = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("BuggyNotes.App");
log.LogInformation("App started");

app.UseDefaultFiles();
app.UseStaticFiles();

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

app.MapPost("/notes", async (AppDb db, ClaimsPrincipal user, Note note) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    note.OwnerId = userId;
    db.Notes.Add(note);
    await db.SaveChangesAsync();
    return Results.Created($"/notes/{note.Id}", note);
}).RequireAuthorization();

app.MapPost("/notes-bug", async (AppDb db, Note note) =>
{
    //accepterar godtycklig OwnerId från klienten
    db.Notes.Add(note);
    await db.SaveChangesAsync();
    return Results.Created($"/notes/{note.Id}", note);
});

app.MapGet("/notes/{id:int}", async (AppDb db, ClaimsPrincipal user, int id) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == userId);
    return note is null ? Results.NotFound() : Results.Ok(note);
}).RequireAuthorization();

app.MapGet("/notes/{id:int}-bug", async (AppDb db, int id) =>
{
    //vem som helst kan läsa vilken note som helst
    var note = await db.Notes.FindAsync(id);
    return note is null ? Results.NotFound() : Results.Ok(note);
});

app.MapGet("/notes/search-bug", async (AppDb db, string q) =>
{
    log.LogWarning("GET /notes/search-bug?q={Q}", q);
    var sql = $"SELECT * FROM Notes WHERE Title LIKE '%{q}%'";
    var list = await db.Notes.FromSqlRaw(sql).ToListAsync();
    log.LogWarning("BUGGY search returned {Count} notes", list.Count);
    return Results.Ok(list);
});

app.MapGet("/notes/search-safe", async (AppDb db, ClaimsPrincipal user, string q) =>
{
    log.LogInformation("GET /notes/search-safe?q={Q}", q);
    
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();
if (string.IsNullOrWhiteSpace(q))
    return Results.Ok(Array.Empty<Note>());

var list = await db.Notes
    .Where(n => n.OwnerId == userId && EF.Functions.Like(n.Title, $"%{q}%"))
    .ToListAsync();

log.LogInformation("Safe search returned {Count} notes", list.Count);
    return Results.Ok(list);
}).RequireAuthorization();


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

app.MapPost("/auth/login-bug", async (AppDb db, LoginDto dto) =>
{
    // varken hash- eller lösenordsverifiering, kräver bara att användaren finns
    var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName);
    if (user is null) return Results.Unauthorized();

    var token = JwtIssuer.CreateToken(user.Id.ToString(), user.UserName, jwtOptions);
    return Results.Ok(new { token, note = "BUG: password was not verified" });
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