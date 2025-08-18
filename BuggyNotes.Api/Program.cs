using Microsoft.EntityFrameworkCore;
using BuggyNotes.Api.Data;
using BuggyNotes.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Seed (för att ha något att söka på första gången)
app.MapPost("/seed", async (AppDb db) =>
{
    if (!db.Notes.Any())
    {
        db.Notes.AddRange(new[] {
            new Note { Title = "Hello", Content = "World" },
            new Note { Title = "Tips", Content = "<b>Bold?</b>" },
            new Note { Title = "SQL", Content = "LIKE and injection" }
        });
        await db.SaveChangesAsync();
    }
    return Results.Ok(new { seeded = true });
});

// Skapa anteckning (superenkel)
app.MapPost("/notes", async (AppDb db, Note note) =>
{
    db.Notes.Add(note);
    await db.SaveChangesAsync();
    return Results.Created($"/notes/{note.Id}", note);
});

// Hämta alla anteckningar
app.MapGet("/notes", async (AppDb db) => await db.Notes.ToListAsync());

// --- A03: Injection (BUGGY) 
// Medvetet sårbar sökning via rå SQL och string-interpolation
// Testa t.ex. q=' OR 1=1 --
app.MapGet("/notes/search-bug", async (AppDb db, string q) =>
{
    var sql = $"SELECT * FROM Notes WHERE Title LIKE '%{q}%'";
    return Results.Ok(await db.Notes.FromSqlRaw(sql).ToListAsync());
});


// Korrekt parameterisering via LINQ/EF.Functions.Like
app.MapGet("/notes/search-safe", async (AppDb db, string q) =>
{
    var list = await db.Notes
        .Where(n => EF.Functions.Like(n.Title, $"%{q}%"))
        .ToListAsync();
    return Results.Ok(list);
});

app.Run();