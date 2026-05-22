namespace WarcraftArchive.Api.Contracts;

public record CreateUserMotiveRequest(string Name, string? Color);
public record UpdateUserMotiveRequest(string Name, string? Color);

public record UserMotiveDto(
    Guid Id,
    string Name,
    string? Color,
    Guid OwnerUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
