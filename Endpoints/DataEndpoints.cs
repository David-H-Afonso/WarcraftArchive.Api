using Microsoft.EntityFrameworkCore;
using System.Text;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.Helpers;
using WarcraftArchive.Api.Models.Auth;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Endpoints;

public static class DataEndpoints
{
    public static void MapDataEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/data").WithTags("Data").RequireAuthorization();

        // ── Export ────────────────────────────────────────────────────────────

        group.MapGet("/export/characters", async (AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var characters = await db.Characters
                .Include(c => c.Warband)
                .Where(c => c.OwnerUserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Name,Class,Race,Level,Covenant,Warband");
            foreach (var c in characters)
                sb.AppendLine($"{Csv(c.Name)},{Csv(c.Class)},{Csv(c.Race)},{c.Level},{Csv(c.Covenant)},{Csv(c.Warband?.Name)}");

            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "characters.csv");
        }).WithName("ExportCharacters").WithSummary("Admin: export characters as CSV");

        group.MapGet("/export/content", async (AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var contents = await db.Contents
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

            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "content.csv");
        }).WithName("ExportContent").WithSummary("Admin: export content as CSV");

        group.MapGet("/export/progress", async (AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var trackings = await db.Trackings
                .Include(t => t.Character)
                .Include(t => t.Content)
                .Where(t => t.Character.OwnerUserId == userId)
                .OrderBy(t => t.Content.Name).ThenBy(t => t.Character.Name)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Character,Content,Difficulty,Frequency,Status,Comment,LastCompletedAt");
            foreach (var t in trackings)
                sb.AppendLine($"{Csv(t.Character.Name)},{Csv(t.Content.Name)},{t.Difficulty},{t.Frequency},{t.Status},{Csv(t.Comment)},{t.LastCompletedAt?.ToString("yyyy-MM-dd") ?? string.Empty}");

            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "progress.csv");
        }).WithName("ExportProgress").WithSummary("Admin: export progress trackings as CSV");

        // ── Import ────────────────────────────────────────────────────────────

        group.MapPost("/import/characters", async (HttpContext ctx, AppDbContext db, HttpRequest req) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var rows = CsvImportHelper.ParseCsvText(text);

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
                if (await db.Characters.AnyAsync(c => c.OwnerUserId == userId && c.Name == name))
                {
                    duplicated++;
                    continue;
                }

                var warbandName = GetColNullable(row, "Warband");
                Guid? warbandId = null;
                if (warbandName != null)
                {
                    var warband = await db.Warbands.FirstOrDefaultAsync(w => w.OwnerUserId == userId && w.Name == warbandName);
                    if (warband == null)
                    {
                        warband = new Warband { Name = warbandName, OwnerUserId = userId.Value };
                        db.Warbands.Add(warband);
                        await db.SaveChangesAsync();
                    }
                    warbandId = warband.Id;
                }

                db.Characters.Add(new Character
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

            await db.SaveChangesAsync();
            return Results.Ok(new { imported, duplicated, errored, errors });
        }).DisableAntiforgery().WithName("ImportCharacters").WithSummary("Admin: import characters from CSV");

        group.MapPost("/import/content", async (HttpContext ctx, AppDbContext db, HttpRequest req) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var rows = CsvImportHelper.ParseCsvText(text);

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

                if (await db.Contents.AnyAsync(c => c.OwnerUserId == userId && c.Name == name && c.Expansion == expansion))
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
                    var m = await db.UserMotives.FirstOrDefaultAsync(x => x.OwnerUserId == userId && x.Name == mn);
                    if (m == null)
                    {
                        m = new UserMotive { Name = mn, OwnerUserId = userId.Value };
                        db.UserMotives.Add(m);
                        await db.SaveChangesAsync();
                    }
                    motives.Add(m);
                }

                db.Contents.Add(new Content
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

            await db.SaveChangesAsync();
            return Results.Ok(new { imported, duplicated, errored, errors });
        }).DisableAntiforgery().WithName("ImportContent").WithSummary("Admin: import content from CSV");

        group.MapPost("/import/progress", async (HttpContext ctx, AppDbContext db, HttpRequest req) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var rows = CsvImportHelper.ParseCsvText(text);
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

                var character = await db.Characters.FirstOrDefaultAsync(c => c.OwnerUserId == userId && c.Name == charName);
                if (character == null)
                {
                    errored++;
                    errors.Add($"Character '{charName}' not found.");
                    continue;
                }

                var content = await db.Contents.FirstOrDefaultAsync(c => c.OwnerUserId == userId && c.Name == contentName);
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

                if (await db.Trackings.AnyAsync(t => t.CharacterId == character.Id && t.ContentId == content.Id && t.Difficulty == difficulty))
                {
                    duplicated++;
                    continue;
                }

                DateTime? lastCompleted = null;
                var lastStr = GetCol(row, "LastCompletedAt", "LastCompleted");
                if (DateTime.TryParse(lastStr, out var lc)) lastCompleted = lc;

                db.Trackings.Add(new Tracking
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

            await db.SaveChangesAsync();
            return Results.Ok(new { imported, duplicated, errored, errors });
        }).DisableAntiforgery().WithName("ImportProgress").WithSummary("Admin: import progress trackings from CSV");
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

    private static async Task<string> ReadBody(HttpRequest req)
    {
        req.EnableBuffering();
        using var reader = new System.IO.StreamReader(req.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        req.Body.Position = 0;
        return body;
    }
}
