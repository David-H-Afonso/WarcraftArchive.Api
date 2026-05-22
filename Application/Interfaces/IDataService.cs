namespace WarcraftArchive.Api.Application.Interfaces;

public record ImportResult(bool Success, int Created, int Updated, int Skipped, string? Error = null, List<string>? Warnings = null);

public interface IDataService
{
    Task<string> ExportCharactersAsync(Guid userId);
    Task<string> ExportContentAsync(Guid userId);
    Task<string> ExportProgressAsync(Guid userId);
    Task<ImportResult> ImportCharactersAsync(Guid userId, string csvText);
    Task<ImportResult> ImportContentAsync(Guid userId, string csvText);
    Task<ImportResult> ImportProgressAsync(Guid userId, string csvText);
}
