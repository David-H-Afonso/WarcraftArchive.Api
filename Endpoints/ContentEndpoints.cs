using WarcraftArchive.Api.DTOs;
using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Services;

namespace WarcraftArchive.Api.Endpoints;

public static class ContentEndpoints
{
    public static void MapContentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/contents").WithTags("Contents").RequireAuthorization();

        group.MapGet("/", async (IContentService service, HttpContext ctx, string? expansion) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            return Results.Ok(await service.GetAllAsync(userId.Value, expansion));
        }).WithName("GetContents").WithSummary("List content for the current user, optionally filtered by expansion");

        group.MapGet("/{id:guid}", async (Guid id, IContentService service) =>
        {
            var content = await service.GetByIdAsync(id);
            return content == null ? Results.NotFound() : Results.Ok(content);
        }).WithName("GetContentById").WithSummary("Get content by ID");

        group.MapPost("/", async (CreateContentRequest request, IContentService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            if (string.IsNullOrWhiteSpace(request.Expansion))
                return Results.BadRequest(new { message = "Expansion is required." });

            var content = await service.CreateAsync(userId.Value, request);
            return Results.Created($"/contents/{content.Id}", content);
        }).WithName("CreateContent").WithSummary("Create a new content entry");

        group.MapPut("/{id:guid}", async (Guid id, UpdateContentRequest request, IContentService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required." });
            if (string.IsNullOrWhiteSpace(request.Expansion))
                return Results.BadRequest(new { message = "Expansion is required." });

            var content = await service.UpdateAsync(id, userId.Value, request);
            return content == null ? Results.NotFound() : Results.Ok(content);
        }).WithName("UpdateContent").WithSummary("Update a content entry");

        group.MapDelete("/{id:guid}", async (Guid id, IContentService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var deleted = await service.DeleteAsync(id, userId.Value);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteContent").WithSummary("Delete a content entry");
    }
}
