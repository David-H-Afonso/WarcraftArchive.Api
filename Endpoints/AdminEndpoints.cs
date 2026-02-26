using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Services;

namespace WarcraftArchive.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin").WithTags("Admin").RequireAuthorization();

        group.MapGet("/users", async (IAuthService authService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            return Results.Ok(await authService.GetAllUsersAsync());
        }).WithName("GetUsers").WithSummary("Admin: list all users");

        group.MapPost("/users", async (CreateUserRequest request, IAuthService authService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { message = "Email and password are required." });

            var user = await authService.CreateUserAsync(
                request.Email, request.UserName, request.Password, request.IsAdmin);
            if (user == null)
                return Results.Conflict(new { message = "A user with that email already exists." });

            return Results.Created($"/admin/users/{user.Id}",
                new UserDto(user.Id, user.Email, user.UserName, user.IsAdmin, user.IsActive,
                    user.CreatedAt, user.UpdatedAt));
        }).WithName("CreateUser").WithSummary("Admin: create a new user");

        group.MapPut("/users/{id:guid}", async (Guid id, UpdateUserRequest request, IAuthService authService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var updated = await authService.UpdateUserAsync(id, request);
            if (updated == null) return Results.NotFound();
            return Results.Ok(updated);
        }).WithName("UpdateUser").WithSummary("Admin: update an existing user");

        // ── Orphans ───────────────────────────────────────────────────────────

        group.MapGet("/orphans", async (AppDbContext db, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();

            var characters = await db.Characters
                .Where(c => c.OwnerUserId == null)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.Class, c.Race, c.Level, c.CreatedAt })
                .ToListAsync();

            var contents = await db.Contents
                .Where(c => c.OwnerUserId == null)
                .OrderBy(c => c.Expansion).ThenBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.Expansion, c.AllowedDifficulties, c.CreatedAt })
                .ToListAsync();

            var trackings = await db.Trackings
                .Include(t => t.Character)
                .Include(t => t.Content)
                .Where(t => t.Character.OwnerUserId == null || t.Content.OwnerUserId == null)
                .OrderBy(t => t.Content.Name).ThenBy(t => t.Character.Name)
                .Select(t => new
                {
                    t.Id,
                    characterId = t.CharacterId,
                    characterName = t.Character.Name,
                    characterOwned = t.Character.OwnerUserId != null,
                    contentId = t.ContentId,
                    contentName = t.Content.Name,
                    contentOwned = t.Content.OwnerUserId != null,
                    t.Difficulty,
                    t.CreatedAt,
                })
                .ToListAsync();

            return Results.Ok(new { characters, contents, trackings });
        }).WithName("GetOrphans").WithSummary("Admin: list all orphaned characters, content and trackings");

        group.MapPost("/orphans/characters/{id:guid}/claim", async (
            Guid id, ClaimOrphanRequest req, AppDbContext db, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();

            var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == null);
            if (character == null) return Results.NotFound();

            var user = await db.Users.FindAsync(req.UserId);
            if (user == null) return Results.BadRequest(new { message = "User not found." });

            character.OwnerUserId = req.UserId;
            await db.SaveChangesAsync();
            return Results.Ok(new { message = $"Character '{character.Name}' claimed by user '{user.UserName}'." });
        }).WithName("ClaimOrphanCharacter").WithSummary("Admin: assign an orphaned character to a user");

        group.MapPost("/orphans/contents/{id:guid}/claim", async (
            Guid id, ClaimOrphanRequest req, AppDbContext db, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();

            var content = await db.Contents.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == null);
            if (content == null) return Results.NotFound();

            var user = await db.Users.FindAsync(req.UserId);
            if (user == null) return Results.BadRequest(new { message = "User not found." });

            content.OwnerUserId = req.UserId;
            await db.SaveChangesAsync();
            return Results.Ok(new { message = $"Content '{content.Name}' claimed by user '{user.UserName}'." });
        }).WithName("ClaimOrphanContent").WithSummary("Admin: assign orphaned content to a user");

        group.MapDelete("/orphans/characters/{id:guid}", async (Guid id, AppDbContext db, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == null);
            if (character == null) return Results.NotFound();
            db.Characters.Remove(character);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithName("DeleteOrphanCharacter").WithSummary("Admin: delete an orphaned character");

        group.MapDelete("/orphans/contents/{id:guid}", async (Guid id, AppDbContext db, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var content = await db.Contents.FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == null);
            if (content == null) return Results.NotFound();
            db.Contents.Remove(content);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithName("DeleteOrphanContent").WithSummary("Admin: delete orphaned content");

        group.MapDelete("/orphans/trackings/{id:guid}", async (Guid id, AppDbContext db, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var tracking = await db.Trackings
                .Include(t => t.Character)
                .Include(t => t.Content)
                .FirstOrDefaultAsync(t => t.Id == id &&
                    (t.Character.OwnerUserId == null || t.Content.OwnerUserId == null));
            if (tracking == null) return Results.NotFound();
            db.Trackings.Remove(tracking);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithName("DeleteOrphanTracking").WithSummary("Admin: delete an orphaned tracking");

        group.MapDelete("/orphans", async (AppDbContext db, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();

            // Delete trackings linked to orphaned characters or content first (FK)
            var orphanTrackings = await db.Trackings
                .Include(t => t.Character)
                .Include(t => t.Content)
                .Where(t => t.Character.OwnerUserId == null || t.Content.OwnerUserId == null)
                .ToListAsync();
            db.Trackings.RemoveRange(orphanTrackings);

            var orphanCharacters = await db.Characters.Where(c => c.OwnerUserId == null).ToListAsync();
            db.Characters.RemoveRange(orphanCharacters);

            var orphanContents = await db.Contents.Where(c => c.OwnerUserId == null).ToListAsync();
            db.Contents.RemoveRange(orphanContents);

            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                deletedTrackings = orphanTrackings.Count,
                deletedCharacters = orphanCharacters.Count,
                deletedContents = orphanContents.Count,
            });
        }).WithName("DeleteAllOrphans").WithSummary("Admin: delete all orphaned characters, content and their trackings");
    }
}

