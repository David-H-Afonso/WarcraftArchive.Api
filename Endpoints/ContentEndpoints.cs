using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Services;

namespace WarcraftArchive.Api.Endpoints;

public static class ContentEndpoints
{
    public static void MapContentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/contents").WithTags("Contents").RequireAuthorization();

        group.MapGet("/", async (IContentService service, string? search) =>
            Results.Ok(await service.GetAllAsync(search))
        ).WithName("GetContents").WithSummary("List content (raids/dungeons/instances), optionally filtered by search term");

        group.MapGet("/{id:guid}", async (Guid id, IContentService service) =>
        {
            var content = await service.GetByIdAsync(id);
            return content == null ? Results.NotFound() : Results.Ok(content);
        }).WithName("GetContentById").WithSummary("Get content by ID");

        group.MapPost("/", async (CreateContentRequest request, IContentService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            if (string.IsNullOrWhiteSpace(request.Expansion))
                return Results.BadRequest(new { message = "Expansion is required." });

            var content = await service.CreateAsync(request);
            return Results.Created($"/contents/{content.Id}", content);
        }).WithName("CreateContent").WithSummary("Create a new content entry");

        group.MapPut("/{id:guid}", async (Guid id, UpdateContentRequest request, IContentService service) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            if (string.IsNullOrWhiteSpace(request.Expansion))
                return Results.BadRequest(new { message = "Expansion is required." });

            var content = await service.UpdateAsync(id, request);
            return content == null ? Results.NotFound() : Results.Ok(content);
        }).WithName("UpdateContent").WithSummary("Update a content entry");

        group.MapDelete("/{id:guid}", async (Guid id, IContentService service) =>
        {
            var deleted = await service.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteContent").WithSummary("Delete a content entry");
    }
}
