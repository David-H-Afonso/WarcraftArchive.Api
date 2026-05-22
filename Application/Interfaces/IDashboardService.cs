using WarcraftArchive.Api.Contracts;

namespace WarcraftArchive.Api.Application.Interfaces;

public interface IDashboardService
{
    Task<WeeklyDashboardDto> GetWeeklyAsync(Guid ownerUserId);
}
