using Microsoft.EntityFrameworkCore;
using BuggyNotes.Api.Data;
using BuggyNotes.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(options =>
    options
        .UseSqlite(builder.Configuration.GetConnectionString("Default"))
        .LogTo(Console.WriteLine, LogLevel.Information) // prints SQL etc.
        .EnableSensitiveDataLogging() // DEV ONLY: visar parameter-värden i logg
);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);


var app = builder.Build();
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

app.Run();