namespace WarcraftArchive.Api.Contracts;

public record CreateCharacterRequest(
    string Name,
    int? Level,
    string Class,
    string? Race,
    string? Covenant,
    Guid? WarbandId,
    Guid? OwnerUserId);

public record UpdateCharacterRequest(
    string Name,
    int? Level,
    string Class,
    string? Race,
    string? Covenant,
    Guid? WarbandId);

public record CharacterDto(
    Guid Id,
    string Name,
    int? Level,
    string Class,
    string? Race,
    string? Covenant,
    Guid? WarbandId,
    string? WarbandName,
    string? WarbandColor,
    Guid? OwnerUserId,
    string? OwnerUserName,
    DateTime CreatedAt,
    DateTime UpdatedAt);
