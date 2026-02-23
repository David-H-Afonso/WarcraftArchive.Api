namespace WarcraftArchive.Api.DTOs;

public record CreateCharacterRequest(
    string Name,
    int? Level,
    string Class,
    string? Covenant,
    string? Warband,
    Guid? OwnerUserId);

public record UpdateCharacterRequest(
    string Name,
    int? Level,
    string Class,
    string? Covenant,
    string? Warband,
    Guid? OwnerUserId);

public record CharacterDto(
    Guid Id,
    string Name,
    int? Level,
    string Class,
    string? Covenant,
    string? Warband,
    Guid? OwnerUserId,
    string? OwnerUserName,
    DateTime CreatedAt,
    DateTime UpdatedAt);
