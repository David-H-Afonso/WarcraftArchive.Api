using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.DTOs;

public record CreateTrackingRequest(
    Guid CharacterId,
    Guid ContentId,
    DifficultyFlags Difficulty,
    Frequency Frequency,
    TrackingStatus Status = TrackingStatus.NotStarted,
    string? Comment = null,
    DateTime? LastCompletedAt = null);

public record UpdateTrackingRequest(
    DifficultyFlags Difficulty,
    Frequency Frequency,
    TrackingStatus Status,
    string? Comment,
    DateTime? LastCompletedAt);

public record TrackingDto(
    Guid Id,
    Guid CharacterId,
    string CharacterName,
    string? CharacterClass,
    string? CharacterRace,
    Guid ContentId,
    string ContentName,
    string Expansion,
    DifficultyFlags Difficulty,
    Frequency Frequency,
    TrackingStatus Status,
    string? Comment,
    DateTime? LastCompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ── Dashboard ─────────────────────────────────────────────────────────────────
public record WeeklyDashboardDto(
    int Total,
    int NotStarted,
    int Pending,
    int InProgress,
    int LastDay,
    int LastWeek,
    int Finished,
    List<TrackingDto> Items);

