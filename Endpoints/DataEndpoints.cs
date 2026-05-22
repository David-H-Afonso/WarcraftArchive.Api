using System.Text;
using WarcraftArchive.Api.Common;
using WarcraftArchive.Api.Application.Interfaces;

namespace WarcraftArchive.Api.Endpoints;

public static class DataEndpoints
{
    public static void MapDataEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/data").WithTags("Data").RequireAuthorization();

        group.MapGet("/export/characters", async (IDataService svc, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var csv = await svc.ExportCharactersAsync(userId.Value);
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "characters.csv");
        }).WithName("ExportCharacters").WithSummary("Admin: export characters as CSV");

        group.MapGet("/export/content", async (IDataService svc, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var csv = await svc.ExportContentAsync(userId.Value);
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "content.csv");
        }).WithName("ExportContent").WithSummary("Admin: export content as CSV");

        group.MapGet("/export/progress", async (IDataService svc, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var csv = await svc.ExportProgressAsync(userId.Value);
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "progress.csv");
        }).WithName("ExportProgress").WithSummary("Admin: export progress trackings as CSV");

        group.MapPost("/import/characters", async (HttpContext ctx, IDataService svc, HttpRequest req) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var result = await svc.ImportCharactersAsync(userId.Value, text);
            return Results.Ok(new { imported = result.Created, duplicated = result.Skipped, errored = result.Warnings?.Count ?? 0, errors = result.Warnings ?? new List<string>() });
        }).DisableAntiforgery().WithName("ImportCharacters").WithSummary("Admin: import characters from CSV");

        group.MapPost("/import/content", async (HttpContext ctx, IDataService svc, HttpRequest req) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var result = await svc.ImportContentAsync(userId.Value, text);
            return Results.Ok(new { imported = result.Created, duplicated = result.Skipped, errored = result.Warnings?.Count ?? 0, errors = result.Warnings ?? new List<string>() });
        }).DisableAntiforgery().WithName("ImportContent").WithSummary("Admin: import content from CSV");

        group.MapPost("/import/progress", async (HttpContext ctx, IDataService svc, HttpRequest req) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var result = await svc.ImportProgressAsync(userId.Value, text);
            return Results.Ok(new { imported = result.Created, duplicated = result.Skipped, errored = result.Warnings?.Count ?? 0, errors = result.Warnings ?? new List<string>() });
        }).DisableAntiforgery().WithName("ImportProgress").WithSummary("Admin: import progress trackings from CSV");
    }

    private static async Task<string> ReadBody(HttpRequest req)
    {
        req.EnableBuffering();
        using var reader = new System.IO.StreamReader(req.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        req.Body.Position = 0;
        return body;
    }
}
