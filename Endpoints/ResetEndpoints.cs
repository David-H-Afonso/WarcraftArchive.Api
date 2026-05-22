using WarcraftArchive.Api.Common;
using WarcraftArchive.Api.Application.Interfaces;

namespace WarcraftArchive.Api.Endpoints;

public static class ResetEndpoints
{
    public static void MapResetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/reset").WithTags("Reset").RequireAuthorization();

        group.MapPost("/daily", async (HttpContext ctx, IResetService resetService) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var affected = await resetService.ApplyDailyResetAsync();
            return Results.Ok(new { affected, message = $"Daily reset applied. {affected} tracking(s) updated." });
        })
        .WithName("TriggerDailyReset")
        .WithSummary("Force a daily reset: Finished→LastDay, LastDay/InProgress/Pending→NotStarted for daily trackings");

        group.MapPost("/weekly", async (HttpContext ctx, IResetService resetService) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var (weekly, daily) = await resetService.ApplyWeeklyResetAsync();
            var total = weekly + daily;
            return Results.Ok(new { affected = total, message = $"Weekly reset applied. {weekly} weekly + {daily} daily tracking(s) updated." });
        })
        .WithName("TriggerWeeklyReset")
        .WithSummary("Force a weekly reset: also runs daily reset");
    }
}
