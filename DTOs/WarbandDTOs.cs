namespace WarcraftArchive.Api.DTOs;

public record CreateWarbandRequest(string Name, string? Color);
public record UpdateWarbandRequest(string Name, string? Color);

public record WarbandDto(
    Guid Id,
    string Name,
    string? Color,
    Guid OwnerUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
