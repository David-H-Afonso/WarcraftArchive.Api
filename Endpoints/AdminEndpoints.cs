using WarcraftArchive.Api.Contracts;
using WarcraftArchive.Api.Common;
using WarcraftArchive.Api.Application.Interfaces;

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

        group.MapDelete("/users/{id:guid}", async (Guid id, IAuthService authService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            if (ctx.GetUserId() == id) return Results.BadRequest(new { message = "You cannot delete your own account." });
            var deleted = await authService.DeleteUserAsync(id);
            if (!deleted) return Results.NotFound();
            return Results.NoContent();
        }).WithName("DeleteUser").WithSummary("Admin: delete a user and all their data");

        group.MapGet("/orphans", async (IAdminService adminService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            return Results.Ok(await adminService.GetOrphansAsync());
        }).WithName("GetOrphans").WithSummary("Admin: list all orphaned characters, content and trackings");

        group.MapPost("/orphans/characters/{id:guid}/claim", async (
            Guid id, ClaimOrphanRequest req, IAdminService adminService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var (success, message) = await adminService.ClaimOrphanCharacterAsync(id, req.UserId);
            if (!success && message == null) return Results.NotFound();
            if (!success) return Results.BadRequest(new { message });
            return Results.Ok(new { message });
        }).WithName("ClaimOrphanCharacter").WithSummary("Admin: assign an orphaned character to a user");

        group.MapPost("/orphans/contents/{id:guid}/claim", async (
            Guid id, ClaimOrphanRequest req, IAdminService adminService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var (success, message) = await adminService.ClaimOrphanContentAsync(id, req.UserId);
            if (!success && message == null) return Results.NotFound();
            if (!success) return Results.BadRequest(new { message });
            return Results.Ok(new { message });
        }).WithName("ClaimOrphanContent").WithSummary("Admin: assign orphaned content to a user");

        group.MapDelete("/orphans/characters/{id:guid}", async (Guid id, IAdminService adminService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            if (!await adminService.DeleteOrphanCharacterAsync(id)) return Results.NotFound();
            return Results.NoContent();
        }).WithName("DeleteOrphanCharacter").WithSummary("Admin: delete an orphaned character");

        group.MapDelete("/orphans/contents/{id:guid}", async (Guid id, IAdminService adminService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            if (!await adminService.DeleteOrphanContentAsync(id)) return Results.NotFound();
            return Results.NoContent();
        }).WithName("DeleteOrphanContent").WithSummary("Admin: delete orphaned content");

        group.MapDelete("/orphans/trackings/{id:guid}", async (Guid id, IAdminService adminService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            if (!await adminService.DeleteOrphanTrackingAsync(id)) return Results.NotFound();
            return Results.NoContent();
        }).WithName("DeleteOrphanTracking").WithSummary("Admin: delete an orphaned tracking");

        group.MapDelete("/orphans", async (IAdminService adminService, HttpContext ctx) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var (deletedTrackings, deletedCharacters, deletedContents) = await adminService.DeleteAllOrphansAsync();
            return Results.Ok(new { deletedTrackings, deletedCharacters, deletedContents });
        }).WithName("DeleteAllOrphans").WithSummary("Admin: delete all orphaned characters, content and their trackings");
    }
}

