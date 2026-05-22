using Microsoft.EntityFrameworkCore;
using System.Text;
using WarcraftArchive.Api.Application.Interfaces;
using WarcraftArchive.Api.Common;
using WarcraftArchive.Api.Infrastructure.Persistence;
using WarcraftArchive.Api.Domain.Entities.Auth;
using WarcraftArchive.Api.Domain.Entities.Warcraft;
using WarcraftArchive.Api.Domain.Enums;

namespace WarcraftArchive.Api.Application.Services;

public class DataService : IDataService
{
    private readonly AppDbContext _db;

    public DataService(AppDbContext db)
    {
        _db = db;
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task<string> ExportCharactersAsync(Guid userId)
    {
        var characters = await _db.Characters
            .Include(c => c.Warband)
            .Where(c => c.OwnerUserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Name,Class,Race,Level,Covenant,Warband");
        foreach (var c in characters)
            sb.AppendLine($"{Csv(c.Name)},{Csv(c.Class)},{Csv(c.Race)},{c.Level},{Csv(c.Covenant)},{Csv(c.Warband?.Name)}");

        return sb.ToString();
    }

    public async Task<string> ExportContentAsync(Guid userId)
    {
        var contents = await _db.Contents
            .Include(c => c.Motives)
            .Where(c => c.OwnerUserId == userId)
            .OrderBy(c => c.Expansion).ThenBy(c => c.Name)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Name,Expansion,Difficulties,Motives,Comment");
        foreach (var c in contents)
        {
            var diffs = FlagsToString((DifficultyFlags)c.AllowedDifficulties);
            var motives = string.Join("|", c.Motives.Select(m => m.Name));
            sb.AppendLine($"{Csv(c.Name)},{Csv(c.Expansion)},{Csv(diffs)},{Csv(motives)},{Csv(c.Comment)}");
        }

        return sb.ToString();
    }

    public async Task<string> ExportProgressAsync(Guid userId)
    {
        var trackings = await _db.Trackings
            .Include(t => t.Character)
            .Include(t => t.Content)
            .Where(t => t.Character.OwnerUserId == userId)
            .OrderBy(t => t.Content.Name).ThenBy(t => t.Character.Name)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Character,Content,Difficulty,Frequency,Status,Comment,LastCompletedAt");
        foreach (var t in trackings)
            sb.AppendLine($"{Csv(t.Character.Name)},{Csv(t.Content.Name)},{t.Difficulty},{t.Frequency},{t.Status},{Csv(t.Comment)},{t.LastCompletedAt?.ToString("yyyy-MM-dd") ?? string.Empty}");

        return sb.ToString();
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportCharactersAsync(Guid userId, string csvText)
    {
        var rows = CsvImportHelper.ParseCsvText(csvText);

        var imported = 0;
        var duplicated = 0;
        var errored = 0;
        var errors = new List<string>();

        foreach (var row in rows)
        {
            var name = GetCol(row, "Name", "Nombre");
            if (string.IsNullOrWhiteSpace(name))
            {
                errored++;
                errors.Add("Row skipped: name is empty.");
                continue;
            }

            // Validate Class
            var cls = GetCol(row, "Class", "Clase");
            if (string.IsNullOrWhiteSpace(cls) || !ValidClasses.Contains(cls))
            {
                errored++;
                errors.Add($"'{name}': invalid class '{cls}'.");
                continue;
            }

            // Validate Race (optional but must be valid if provided)
            var raceRaw = GetColNullable(row, "Race", "Raza");
            if (raceRaw != null && !ValidRaces.Contains(raceRaw))
            {
                errored++;
                errors.Add($"'{name}': invalid race '{raceRaw}'.");
                continue;
            }

            // Validate Level (optional but must be 1-80 if provided)
            int? level = null;
            var levelStr = GetCol(row, "Level", "Nivel");
            if (!string.IsNullOrWhiteSpace(levelStr))
            {
                if (!int.TryParse(levelStr, out var lv) || lv < 1 || lv > 80)
                {
                    errored++;
                    errors.Add($"'{name}': invalid level '{levelStr}' (must be 1-80).");
                    continue;
                }
                level = lv;
            }

            // Check duplicate
            if (await _db.Characters.AnyAsync(c => c.OwnerUserId == userId && c.Name == name))
            {
                duplicated++;
                continue;
            }

            var warbandName = GetColNullable(row, "Warband");
            Guid? warbandId = null;
            if (warbandName != null)
            {
                var warband = await _db.Warbands.FirstOrDefaultAsync(w => w.OwnerUserId == userId && w.Name == warbandName);
                if (warband == null)
                {
                    warband = new Warband { Name = warbandName, OwnerUserId = userId };
                    _db.Warbands.Add(warband);
                    await _db.SaveChangesAsync();
                }
                warbandId = warband.Id;
            }

            _db.Characters.Add(new Character
            {
                Name = name,
                Class = cls,
                Race = raceRaw,
                Level = level,
                Covenant = GetColNullable(row, "Covenant", "Pacto"),
                WarbandId = warbandId,
                OwnerUserId = userId,
            });
            imported++;
        }

        await _db.SaveChangesAsync();
        return new ImportResult(true, imported, 0, duplicated, Warnings: errors.Count > 0 ? errors : null);
    }

    public async Task<ImportResult> ImportContentAsync(Guid userId, string csvText)
    {
        var rows = CsvImportHelper.ParseCsvText(csvText);

        var imported = 0;
        var duplicated = 0;
        var errored = 0;
        var errors = new List<string>();

        foreach (var row in rows)
        {
            var name = GetCol(row, "Name", "Nombre");
            if (string.IsNullOrWhiteSpace(name))
            {
                errored++;
                errors.Add("Row skipped: name is empty.");
                continue;
            }

            var expansion = GetCol(row, "Expansion");
            if (string.IsNullOrWhiteSpace(expansion))
            {
                errored++;
                errors.Add($"'{name}': expansion is required.");
                continue;
            }

            var diffStr = GetCol(row, "Difficulties", "Dificultades", "AllowedDifficulties");
            var allowedDifficulties = (int)CsvImportHelper.ParseDifficultyFlags(diffStr);
            if (allowedDifficulties == 0)
            {
                errored++;
                errors.Add($"'{name}': no valid difficulties found in '{diffStr}'. Use LFR, Normal, Heroic, Mythic separated by |.");
                continue;
            }

            if (await _db.Contents.AnyAsync(c => c.OwnerUserId == userId && c.Name == name && c.Expansion == expansion))
            {
                duplicated++;
                continue;
            }

            var motiveStr = GetCol(row, "Motives", "Motivos");
            var motives = new List<UserMotive>();
            foreach (var part in motiveStr.Split('|', ',', ';'))
            {
                var mn = part.Trim();
                if (string.IsNullOrWhiteSpace(mn)) continue;
                var m = await _db.UserMotives.FirstOrDefaultAsync(x => x.OwnerUserId == userId && x.Name == mn);
                if (m == null)
                {
                    m = new UserMotive { Name = mn, OwnerUserId = userId };
                    _db.UserMotives.Add(m);
                    await _db.SaveChangesAsync();
                }
                motives.Add(m);
            }

            _db.Contents.Add(new Content
            {
                Name = name,
                Expansion = expansion,
                Comment = GetColNullable(row, "Comment", "Comentario"),
                AllowedDifficulties = allowedDifficulties,
                Motives = motives,
                OwnerUserId = userId,
            });
            imported++;
        }

        await _db.SaveChangesAsync();
        return new ImportResult(true, imported, 0, duplicated, Warnings: errors.Count > 0 ? errors : null);
    }

    public async Task<ImportResult> ImportProgressAsync(Guid userId, string csvText)
    {
        var rows = CsvImportHelper.ParseCsvText(csvText);
        var imported = 0;
        var duplicated = 0;
        var errored = 0;
        var errors = new List<string>();

        foreach (var row in rows)
        {
            var charName = GetCol(row, "Character", "Personaje");
            var contentName = GetCol(row, "Content", "Contenido");
            if (string.IsNullOrWhiteSpace(charName) || string.IsNullOrWhiteSpace(contentName))
            {
                errored++;
                errors.Add("Row skipped: character or content name is empty.");
                continue;
            }

            var character = await _db.Characters.FirstOrDefaultAsync(c => c.OwnerUserId == userId && c.Name == charName);
            if (character == null)
            {
                errored++;
                errors.Add($"Character '{charName}' not found.");
                continue;
            }

            var content = await _db.Contents.FirstOrDefaultAsync(c => c.OwnerUserId == userId && c.Name == contentName);
            if (content == null)
            {
                errored++;
                errors.Add($"Content '{contentName}' not found.");
                continue;
            }

            var diffStr = GetCol(row, "Difficulty", "Dificultad");
            var difficulty = CsvImportHelper.ParseDifficulty(diffStr);
            if ((content.AllowedDifficulties & (int)difficulty) == 0)
            {
                errored++;
                errors.Add($"'{charName}' / '{contentName}': difficulty '{diffStr}' is not allowed for this content.");
                continue;
            }

            if (await _db.Trackings.AnyAsync(t => t.CharacterId == character.Id && t.ContentId == content.Id && t.Difficulty == difficulty))
            {
                duplicated++;
                continue;
            }

            DateTime? lastCompleted = null;
            var lastStr = GetCol(row, "LastCompletedAt", "LastCompleted");
            if (DateTime.TryParse(lastStr, out var lc)) lastCompleted = lc;

            _db.Trackings.Add(new Tracking
            {
                CharacterId = character.Id,
                ContentId = content.Id,
                Difficulty = difficulty,
                Frequency = CsvImportHelper.ParseFrequency(GetCol(row, "Frequency", "Frecuencia")),
                Status = CsvImportHelper.ParseTrackingStatus(GetCol(row, "Status", "Estado")),
                Comment = GetColNullable(row, "Comment", "Comentario"),
                LastCompletedAt = lastCompleted,
            });
            imported++;
        }

        await _db.SaveChangesAsync();
        return new ImportResult(true, imported, 0, duplicated, Warnings: errors.Count > 0 ? errors : null);
    }

    // ── Valid WoW class/race lists (mirror wowConstants.ts) ───────────────────

    private static readonly HashSet<string> ValidClasses = new(StringComparer.Ordinal)
    {
        "Death Knight", "Demon Hunter", "Druid", "Evoker", "Hunter", "Mage", "Monk",
        "Paladin", "Priest", "Rogue", "Shaman", "Warlock", "Warrior",
    };

    private static readonly HashSet<string> ValidRaces = new(StringComparer.Ordinal)
    {
        // Alliance
        "Human", "Dwarf", "Night Elf", "Gnome", "Draenei", "Worgen",
        "Void Elf", "Lightforged Draenei", "Dark Iron Dwarf", "Kul Tiran", "Mechagnome",
        // Horde
        "Orc", "Undead", "Tauren", "Troll", "Blood Elf", "Goblin",
        "Nightborne", "Highmountain Tauren", "Mag'har Orc", "Zandalari Troll", "Vulpera",
        // Neutral
        "Pandaren", "Dracthyr", "Earthen",
    };

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string Csv(string? v)
    {
        if (v == null) return string.Empty;
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }

    private static string GetCol(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var k in keys)
            if (row.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        return string.Empty;
    }

    private static string? GetColNullable(Dictionary<string, string> row, params string[] keys)
    {
        var v = GetCol(row, keys);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string FlagsToString(DifficultyFlags flags)
    {
        var parts = new List<string>();
        if (flags.HasFlag(DifficultyFlags.LFR)) parts.Add("LFR");
        if (flags.HasFlag(DifficultyFlags.Normal)) parts.Add("Normal");
        if (flags.HasFlag(DifficultyFlags.Heroic)) parts.Add("Heroic");
        if (flags.HasFlag(DifficultyFlags.Mythic)) parts.Add("Mythic");
        return string.Join("|", parts);
    }
}
