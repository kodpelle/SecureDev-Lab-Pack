using Microsoft.EntityFrameworkCore;
using BuggyNotes.Api.Data;
using BuggyNotes.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BuggyNotes.Api.Auth;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using BuggyNotes.Api.Crypto;
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

//      %' OR 1=1 --     
app.MapGet("/notes/search-bug", async (AppDb db, ClaimsPrincipal user, string q) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();

    var list = await db.Notes
        .FromSqlRaw($"SELECT * FROM Notes WHERE OwnerId = '{userId}' AND Title LIKE '%{q}%'")
        .ToListAsync();

    return Results.Ok(list);
}).RequireAuthorization();

app.MapGet("/notes/search-safe", async (AppDb db, ClaimsPrincipal user, string q) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (userId is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<Note>());

    var list = await db.Notes
        .Where(n => n.OwnerId == userId && EF.Functions.Like(n.Title, $"%{q}%"))
        .ToListAsync();

    return Results.Ok(list);
}).RequireAuthorization();

// AES-GCM (safe)
app.MapPost("/crypto/aes/gcm/encrypt", (AesEncryptRequest req)
    => Results.Ok(CryptoService.AesGcmEncrypt(req.Plaintext, req.Base64Key)));

app.MapPost("/crypto/aes/gcm/decrypt", (AesDecryptRequest req)
    => Results.Ok(CryptoService.AesGcmDecrypt(req.Base64Key, req.Base64Nonce, req.Base64Ciphertext, req.Base64Tag)));

// AES-CBC (bug)
app.MapPost("/crypto/aes/cbc-bug/encrypt", (AesEncryptRequest req)
    => Results.Ok(CryptoService.AesCbcInsecureEncrypt(req.Plaintext, req.Base64Key)));

// PBKDF2 (safe)
app.MapPost("/crypto/hash/pbkdf2", (HashRequest req)
    => Results.Ok(CryptoService.HashPasswordPbkdf2(req.Password, req.Iterations)));

app.MapPost("/crypto/hash/verify", (VerifyRequest req)
    => Results.Ok(CryptoService.VerifyPasswordPbkdf2(req.Password, req.HashBase64, req.Iterations)));

// SHA-256 (bug)
app.MapPost("/crypto/hash/sha256-bug", (HashRequest req)
    => Results.Ok(CryptoService.HashPasswordSha256(req.Password)));


app.MapGet("/__routes", (IEnumerable<EndpointDataSource> sources) =>
{
    var list = new List<string>();
    foreach (var s in sources)
        foreach (var e in s.Endpoints)
            list.Add(e.DisplayName ?? "(no name)");
    return Results.Ok(list);
});

app.Run();