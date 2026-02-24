using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Services;

namespace WarcraftArchive.Api.Endpoints;

public static class UserMotiveEndpoints
{
    public static void MapUserMotiveEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/motives").WithTags("Motives").RequireAuthorization();

        group.MapGet("/", async (IUserMotiveService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            return Results.Ok(await service.GetAllAsync(userId.Value));
        }).WithName("GetUserMotives").WithSummary("List motives for the current user");

        group.MapGet("/{id:guid}", async (Guid id, IUserMotiveService service) =>
        {
            var m = await service.GetByIdAsync(id);
            return m == null ? Results.NotFound() : Results.Ok(m);
        }).WithName("GetUserMotiveById").WithSummary("Get motive by ID");

        group.MapPost("/", async (CreateUserMotiveRequest request, IUserMotiveService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            var (dto, error) = await service.CreateAsync(userId.Value, request);
            if (error != null) return Results.Conflict(new { message = error });
            return Results.Created($"/motives/{dto!.Id}", dto);
        }).WithName("CreateUserMotive").WithSummary("Create a motive for the current user");

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserMotiveRequest request, IUserMotiveService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            var dto = await service.UpdateAsync(id, userId.Value, request);
            return dto == null ? Results.NotFound() : Results.Ok(dto);
        }).WithName("UpdateUserMotive").WithSummary("Update a motive");

        group.MapDelete("/{id:guid}", async (Guid id, IUserMotiveService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var deleted = await service.DeleteAsync(id, userId.Value);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteUserMotive").WithSummary("Delete a motive");
    }
}
