namespace WarcraftArchive.Api.Application.Interfaces;

public interface IResetService
{
    Task<int> ApplyDailyResetAsync();
    Task<(int Weekly, int Daily)> ApplyWeeklyResetAsync();
}
