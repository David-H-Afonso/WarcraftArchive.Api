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
    }
}

