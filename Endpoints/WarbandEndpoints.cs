using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Services;

namespace WarcraftArchive.Api.Endpoints;

public static class WarbandEndpoints
{
    public static void MapWarbandEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/warbands").WithTags("Warbands").RequireAuthorization();

        group.MapGet("/", async (IWarbandService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            return Results.Ok(await service.GetAllAsync(userId.Value));
        }).WithName("GetWarbands").WithSummary("List warbands for the current user");

        group.MapGet("/{id:guid}", async (Guid id, IWarbandService service) =>
        {
            var w = await service.GetByIdAsync(id);
            return w == null ? Results.NotFound() : Results.Ok(w);
        }).WithName("GetWarbandById").WithSummary("Get warband by ID");

        group.MapPost("/", async (CreateWarbandRequest request, IWarbandService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            if (request.Name.Length > 12)
                return Results.BadRequest(new { message = "Warband name must be 12 characters or fewer." });
            var (dto, error) = await service.CreateAsync(userId.Value, request);
            if (error != null) return Results.Conflict(new { message = error });
            return Results.Created($"/warbands/{dto!.Id}", dto);
        }).WithName("CreateWarband").WithSummary("Create a warband for the current user");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWarbandRequest request, IWarbandService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            if (request.Name.Length > 12)
                return Results.BadRequest(new { message = "Warband name must be 12 characters or fewer." });
            var dto = await service.UpdateAsync(id, userId.Value, request);
            return dto == null ? Results.NotFound() : Results.Ok(dto);
        }).WithName("UpdateWarband").WithSummary("Update a warband");

        group.MapDelete("/{id:guid}", async (Guid id, IWarbandService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var deleted = await service.DeleteAsync(id, userId.Value);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteWarband").WithSummary("Delete a warband");
    }
}
