using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Services;

namespace WarcraftArchive.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/dashboard").WithTags("Dashboard").RequireAuthorization();

        group.MapGet("/weekly", async (IDashboardService service, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            return Results.Ok(await service.GetWeeklyAsync(userId.Value));
        }).WithName("GetWeeklyDashboard")
         .WithSummary("Aggregated summary of Weekly trackings grouped by status — ideal for home screen");
    }
}
