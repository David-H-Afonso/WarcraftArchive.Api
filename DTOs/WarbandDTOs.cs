namespace WarcraftArchive.Api.DTOs;

public record CreateWarbandRequest(string Name, string? Color);
public record UpdateWarbandRequest(string Name, string? Color);
public record ReorderWarbandItem(Guid Id, int SortOrder);

public record WarbandDto(
    Guid Id,
    string Name,
    string? Color,
    int SortOrder,
    Guid OwnerUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
