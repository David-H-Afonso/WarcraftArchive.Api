namespace WarcraftArchive.Api.DTOs;

public record CreateContentRequest(
    string Name,
    string Expansion,
    string? Comment,
    int AllowedDifficulties,
    List<Guid> MotiveIds);

public record UpdateContentRequest(
    string Name,
    string Expansion,
    string? Comment,
    int AllowedDifficulties,
    List<Guid> MotiveIds);

public record ContentDto(
    Guid Id,
    string Name,
    string Expansion,
    string? Comment,
    int AllowedDifficulties,
    List<UserMotiveDto> Motives,
    DateTime CreatedAt,
    DateTime UpdatedAt);
