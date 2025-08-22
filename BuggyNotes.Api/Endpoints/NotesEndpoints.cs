using System.Security.Claims;
using BuggyNotes.Api.Data;
using BuggyNotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;

namespace BuggyNotes.Api.Endpoints;
public class NotesEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var safe = app.MapGroup("/notes").RequireAuthorization();
        var bug = app.MapGroup("/notes-bug");

        safe.MapPost("/", async (AppDb db, ClaimsPrincipal user, Note note) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            note.OwnerId = userId;
            db.Notes.Add(note);
            await db.SaveChangesAsync();
            return Results.Created($"/notes/{note.Id}", note);
        });

        bug.MapPost("/", async (AppDb db, Note note) =>
        {
            db.Notes.Add(note);
            await db.SaveChangesAsync();
            return Results.Created($"/notes/{note.Id}", note);
        });

        safe.MapGet("/{id:int}", async (AppDb db, ClaimsPrincipal user, int id) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == userId);
            return note is null ? Results.NotFound() : Results.Ok(note);
        });

        bug.MapGet("/{id:int}", async (AppDb db, int id) =>
        {
            var note = await db.Notes.FindAsync(id);
            return note is null ? Results.NotFound() : Results.Ok(note);
        });

        // BUG: SQLi: %' OR 1=1 --
        safe.MapGet("/search-bug", async (AppDb db, ClaimsPrincipal user, string q) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();

            var list = await db.Notes
                .FromSqlRaw($"SELECT * FROM Notes WHERE OwnerId = '{userId}' AND Title LIKE '%{q}%'")
                .ToListAsync();

            return Results.Ok(list);
        });

        // SAFE
        safe.MapGet("/search-safe", async (AppDb db, ClaimsPrincipal user, string q) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<Note>());

            var list = await db.Notes
                .Where(n => n.OwnerId == userId && EF.Functions.Like(n.Title, $"%{q}%"))
                .ToListAsync();

            return Results.Ok(list);
        });
    }
}

