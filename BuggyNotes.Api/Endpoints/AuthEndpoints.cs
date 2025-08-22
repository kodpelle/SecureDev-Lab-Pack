using System.Security.Claims;
using BuggyNotes.Api.Auth;
using BuggyNotes.Api.Data;
using BuggyNotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;

namespace BuggyNotes.Api.Endpoints;

public class AuthEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var jwtOptions = app.ServiceProvider.GetRequiredService<JwtOptions>();
        var grp = app.MapGroup("/auth");

        grp.MapPost("/register", async (AppDb db, RegisterDto dto) =>
        {
            var hasher = new PasswordHasher<User>();
            if (await db.Users.AnyAsync(u => u.UserName == dto.UserName))
                return Results.BadRequest(new { message = "User already exists" });

            var user = new User { UserName = dto.UserName };
            user.PasswordHash = hasher.HashPassword(user, dto.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/users/{user.Id}", new { user.Id, user.UserName });
        });

        grp.MapPost("/login", async (AppDb db, LoginDto dto) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName);
            if (user is null) return Results.Unauthorized();

            var hasher = new PasswordHasher<User>();
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            var token = JwtIssuer.CreateToken(user.Id.ToString(), user.UserName, jwtOptions);
            return Results.Ok(new { token });
        });

        grp.MapPost("/login-bug", async (AppDb db, LoginDto dto) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == dto.UserName);
            if (user is null) return Results.Unauthorized();
            var token = JwtIssuer.CreateToken(user.Id.ToString(), user.UserName, jwtOptions);
            return Results.Ok(new { token, note = "BUG: password was not verified" });
        });

        app.MapGet("/me", [Authorize] (ClaimsPrincipal user) =>
        {
            var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "?";
            var name = user.Identity?.Name ?? "?";
            return Results.Ok(new { id, name });
        });
    }
}

