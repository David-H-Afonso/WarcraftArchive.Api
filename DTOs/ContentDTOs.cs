using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.DTOs;

public record CreateContentRequest(
    string Name,
    string Expansion,
    string? Comment,
    DifficultyFlags AllowedDifficulties,
    MotiveFlags Motives);

public record UpdateContentRequest(
    string Name,
    string Expansion,
    string? Comment,
    DifficultyFlags AllowedDifficulties,
    MotiveFlags Motives);

public record ContentDto(
    Guid Id,
    string Name,
    string Expansion,
    string? Comment,
    DifficultyFlags AllowedDifficulties,
    MotiveFlags Motives,
    DateTime CreatedAt,
    DateTime UpdatedAt);
