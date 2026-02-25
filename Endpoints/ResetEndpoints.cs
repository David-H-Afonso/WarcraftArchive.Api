using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Endpoints;

public static class ResetEndpoints
{
    public static void MapResetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/reset").WithTags("Reset").RequireAuthorization();

        group.MapPost("/daily", async (HttpContext ctx, AppDbContext db) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            var affected = await ApplyDailyReset(db);
            return Results.Ok(new { affected, message = $"Daily reset applied. {affected} tracking(s) updated." });
        })
        .WithName("TriggerDailyReset")
        .WithSummary("Force a daily reset: Finished→LastDay, LastDay→NotStarted for daily trackings");

        group.MapPost("/weekly", async (HttpContext ctx, AppDbContext db) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();
            // A weekly reset also implies a daily reset
            var weekly = await ApplyWeeklyReset(db);
            var daily = await ApplyDailyReset(db);
            var total = weekly + daily;
            return Results.Ok(new { affected = total, message = $"Weekly reset applied. {weekly} weekly + {daily} daily tracking(s) updated." });
        })
        .WithName("TriggerWeeklyReset")
        .WithSummary("Force a weekly reset: also runs daily reset");
    }

    /// <summary>Daily reset: Finished→LastDay, LastDay→NotStarted for daily trackings.</summary>
    private static async Task<int> ApplyDailyReset(AppDbContext db)
    {
        var trackings = await db.Trackings
            .Where(t => t.Frequency == Frequency.Daily &&
                        (t.Status == TrackingStatus.Finished || t.Status == TrackingStatus.LastDay))
            .ToListAsync();

        foreach (var t in trackings)
            t.Status = t.Status == TrackingStatus.Finished
                ? TrackingStatus.LastDay
                : TrackingStatus.NotStarted;

        if (trackings.Count > 0)
            await db.SaveChangesAsync();

        return trackings.Count;
    }

    /// <summary>Weekly reset: Finished→LastWeek, LastWeek→NotStarted for weekly trackings.</summary>
    private static async Task<int> ApplyWeeklyReset(AppDbContext db)
    {
        var trackings = await db.Trackings
            .Where(t => t.Frequency == Frequency.Weekly &&
                        (t.Status == TrackingStatus.Finished || t.Status == TrackingStatus.LastWeek))
            .ToListAsync();

        foreach (var t in trackings)
            t.Status = t.Status == TrackingStatus.Finished
                ? TrackingStatus.LastWeek
                : TrackingStatus.NotStarted;

        if (trackings.Count > 0)
            await db.SaveChangesAsync();

        return trackings.Count;
    }
}
