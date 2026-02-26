using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Models.Warcraft;
using WarcraftArchive.Api.Services;

namespace WarcraftArchive.Api.Endpoints;

public static class TrackingEndpoints
{
    public static void MapTrackingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/trackings").WithTags("Trackings").RequireAuthorization();

        group.MapGet("/", async (
            ITrackingService service,
            HttpContext ctx,
            Guid? characterId,
            TrackingStatus? status,
            Frequency? frequency,
            string? expansion,
            Guid? motiveId,
            Guid? contentId) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            return Results.Ok(await service.GetAllAsync(userId.Value, characterId, status, frequency, expansion, motiveId, contentId));
        }).WithName("GetTrackings")
         .WithSummary("List trackings with optional filters: characterId, status, frequency, expansion, motiveId, contentId");

        group.MapGet("/{id:guid}", async (Guid id, ITrackingService service) =>
        {
            var tracking = await service.GetByIdAsync(id);
            return tracking == null ? Results.NotFound() : Results.Ok(tracking);
        }).WithName("GetTrackingById").WithSummary("Get a tracking entry by ID");

        group.MapPost("/", async (CreateTrackingRequest request, ITrackingService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var (dto, error) = await service.CreateAsync(userId.Value, request);
            if (error != null) return Results.BadRequest(new { message = error });
            return Results.Created($"/trackings/{dto!.Id}", dto);
        }).WithName("CreateTracking").WithSummary("Create a new tracking entry for a character+content+difficulty");

        group.MapPut("/{id:guid}", async (Guid id, UpdateTrackingRequest request, ITrackingService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var (dto, error) = await service.UpdateAsync(id, userId.Value, request);
            if (dto == null && error == null) return Results.NotFound();
            if (error != null) return Results.BadRequest(new { message = error });
            return Results.Ok(dto);
        }).WithName("UpdateTracking").WithSummary("Update a tracking entry");

        group.MapDelete("/{id:guid}", async (Guid id, ITrackingService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var deleted = await service.DeleteAsync(id, userId.Value);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteTracking").WithSummary("Delete a tracking entry");
    }
}
