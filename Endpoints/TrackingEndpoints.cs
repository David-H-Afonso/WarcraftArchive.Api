using WarcraftArchive.Api.DTOs;
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
            Guid? characterId,
            TrackingStatus? status,
            Frequency? frequency,
            string? expansion,
            Guid? motiveId,
            Guid? contentId) =>
            Results.Ok(await service.GetAllAsync(characterId, status, frequency, expansion, motiveId, contentId))
        ).WithName("GetTrackings")
         .WithSummary("List trackings with optional filters: characterId, status, frequency, expansion, motiveId, contentId");

        group.MapGet("/{id:guid}", async (Guid id, ITrackingService service) =>
        {
            var tracking = await service.GetByIdAsync(id);
            return tracking == null ? Results.NotFound() : Results.Ok(tracking);
        }).WithName("GetTrackingById").WithSummary("Get a tracking entry by ID");

        group.MapPost("/", async (CreateTrackingRequest request, ITrackingService service) =>
        {
            var (dto, error) = await service.CreateAsync(request);
            if (error != null) return Results.BadRequest(new { message = error });
            return Results.Created($"/trackings/{dto!.Id}", dto);
        }).WithName("CreateTracking").WithSummary("Create a new tracking entry for a character+content+difficulty");

        group.MapPut("/{id:guid}", async (Guid id, UpdateTrackingRequest request, ITrackingService service) =>
        {
            var (dto, error) = await service.UpdateAsync(id, request);
            if (dto == null && error == null) return Results.NotFound();
            if (error != null) return Results.BadRequest(new { message = error });
            return Results.Ok(dto);
        }).WithName("UpdateTracking").WithSummary("Update a tracking entry");

        group.MapDelete("/{id:guid}", async (Guid id, ITrackingService service) =>
        {
            var deleted = await service.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteTracking").WithSummary("Delete a tracking entry");
    }
}
