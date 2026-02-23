using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WarcraftArchive.Api.Data;
using WarcraftArchive.Api.Models.Auth;
using WarcraftArchive.Api.Models.Warcraft;

namespace WarcraftArchive.Api.Helpers;

/// <summary>
/// Imports demo data from Notion-exported CSV files.
/// Files are looked up by glob patterns inside a configurable directory.
/// The import is idempotent: rows already in the DB are skipped.
/// </summary>
public static class CsvImportHelper
{
    // ── Public entry point ────────────────────────────────────────────────────

    public static async Task ImportAsync(AppDbContext db, string csvDataPath, User adminUser, ILogger logger)
    {
        if (!Directory.Exists(csvDataPath))
        {
            logger.LogWarning("CsvImport: data path '{Path}' does not exist — skipping.", csvDataPath);
            return;
        }

        logger.LogInformation("CsvImport: scanning '{Path}' for CSV files…", csvDataPath);

        var charFile    = FindFile(csvDataPath, "Personajes");
        var contentFile = FindFile(csvDataPath, "Raids") ?? FindFile(csvDataPath, "dungeons") ?? FindFile(csvDataPath, "instances");
        var progressFile = FindFile(csvDataPath, "Content Progress") ?? FindFile(csvDataPath, "Progress");

        if (charFile != null)
            await ImportCharactersAsync(db, charFile, adminUser, logger);

        if (contentFile != null)
            await ImportContentsAsync(db, contentFile, logger);

        if (progressFile != null)
            await ImportTrackingsAsync(db, progressFile, db, logger);

        logger.LogInformation("CsvImport: done.");
    }

    // ── Characters ────────────────────────────────────────────────────────────

    private static async Task ImportCharactersAsync(AppDbContext db, string filePath, User adminUser, ILogger logger)
    {
        logger.LogInformation("CsvImport: importing characters from '{File}'", filePath);
        var rows = ParseCsv(filePath);
        var imported = 0;

        foreach (var row in rows)
        {
            var name = ExtractName(GetColumn(row, "Name", "Nombre", "Character", "Personaje"));
            if (string.IsNullOrWhiteSpace(name)) continue;

            var alreadyExists = await db.Characters.AnyAsync(c => c.Name == name);
            if (alreadyExists) continue;

            var levelStr = GetColumn(row, "Level", "Nivel");
            int? level = int.TryParse(levelStr, out var lv) ? lv : null;

            var character = new Character
            {
                Name = name,
                Level = level,
                Class = GetColumn(row, "Class", "Clase") is { Length: > 0 } cls ? cls : "Unknown",
                Covenant = NullIfEmpty(GetColumn(row, "Covenant", "Pacto")),
                Warband = NullIfEmpty(GetColumn(row, "Warband", "Banda de guerra", "Grupo")),
                OwnerUserId = adminUser.Id,
            };
            db.Characters.Add(character);
            imported++;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("CsvImport: {Count} characters imported.", imported);
    }

    // ── Contents ──────────────────────────────────────────────────────────────

    private static async Task ImportContentsAsync(AppDbContext db, string filePath, ILogger logger)
    {
        logger.LogInformation("CsvImport: importing contents from '{File}'", filePath);
        var rows = ParseCsv(filePath);
        var imported = 0;

        foreach (var row in rows)
        {
            var name = ExtractName(GetColumn(row, "Name", "Nombre", "Instance", "Raid", "Dungeon"));
            if (string.IsNullOrWhiteSpace(name)) continue;

            var expansion = GetColumn(row, "Expansion", "Expansión", "Expansion Name");
            if (string.IsNullOrWhiteSpace(expansion)) expansion = "Unknown";

            var alreadyExists = await db.Contents.AnyAsync(c => c.Name == name && c.Expansion == expansion);
            if (alreadyExists) continue;

            var diffStr = GetColumn(row, "Difficulties", "Dificultades", "Difficulty", "Dificultad", "AllowedDifficulties");
            var motiveStr = GetColumn(row, "Motives", "Motivos", "Motive", "Motivo", "Goals", "Objetivo");
            var comment = NullIfEmpty(GetColumn(row, "Comment", "Comentario", "Notes", "Notas"));

            var content = new Content
            {
                Name = name,
                Expansion = expansion,
                Comment = comment,
                AllowedDifficulties = (int)ParseDifficultyFlags(diffStr),
                Motives = (int)ParseMotiveFlags(motiveStr),
            };
            db.Contents.Add(content);
            imported++;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("CsvImport: {Count} contents imported.", imported);
    }

    // ── Trackings ─────────────────────────────────────────────────────────────

    private static async Task ImportTrackingsAsync(AppDbContext db, string filePath, AppDbContext _, ILogger logger)
    {
        logger.LogInformation("CsvImport: importing trackings from '{File}'", filePath);
        var rows = ParseCsv(filePath);
        var imported = 0;
        var skipped  = 0;

        foreach (var row in rows)
        {
            var charName    = ExtractName(GetColumn(row, "Character", "Personaje", "Char", "Name"));
            var contentName = ExtractName(GetColumn(row, "Content", "Contenido", "Instance", "Raid"));

            if (string.IsNullOrWhiteSpace(charName) || string.IsNullOrWhiteSpace(contentName))
            {
                skipped++;
                continue;
            }

            var character = await db.Characters.FirstOrDefaultAsync(c => c.Name == charName);
            if (character == null)
            {
                logger.LogDebug("CsvImport: character '{Name}' not found — skipping row.", charName);
                skipped++;
                continue;
            }

            var content = await db.Contents.FirstOrDefaultAsync(c => c.Name == contentName);
            if (content == null)
            {
                logger.LogDebug("CsvImport: content '{Name}' not found — skipping row.", contentName);
                skipped++;
                continue;
            }

            var diffStr      = GetColumn(row, "Difficulty", "Dificultad");
            var freqStr      = GetColumn(row, "Frequency", "Frecuencia");
            var statusStr    = GetColumn(row, "Status", "Estado");
            var comment      = NullIfEmpty(GetColumn(row, "Comment", "Comentario", "Notes", "Notas"));

            var difficulty = ParseDifficulty(diffStr);
            var frequency  = ParseFrequency(freqStr);
            var status     = ParseTrackingStatus(statusStr);

            // Idempotency check
            var exists = await db.Trackings.AnyAsync(t =>
                t.CharacterId == character.Id &&
                t.ContentId   == content.Id  &&
                t.Difficulty  == difficulty);
            if (exists)
            {
                skipped++;
                continue;
            }

            db.Trackings.Add(new Tracking
            {
                CharacterId = character.Id,
                ContentId   = content.Id,
                Difficulty  = difficulty,
                Frequency   = frequency,
                Status      = status,
                Comment     = comment,
            });
            imported++;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("CsvImport: {Count} trackings imported, {Skipped} skipped.", imported, skipped);
    }

    // ── CSV parser ────────────────────────────────────────────────────────────

    /// <summary>Minimal RFC-4180-compatible CSV parser. Returns rows as dictionaries keyed by header name.</summary>
    internal static List<Dictionary<string, string>> ParseCsv(string filePath)
    {
        var text    = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        var lines   = SplitCsvLines(text);
        var result  = new List<Dictionary<string, string>>();
        if (lines.Count == 0) return result;

        var headers = SplitCsvRow(lines[0]);
        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var values = SplitCsvRow(lines[i]);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Count; j++)
                row[headers[j].Trim()] = j < values.Count ? values[j].Trim() : string.Empty;
            result.Add(row);
        }
        return result;
    }

    private static List<string> SplitCsvLines(string text)
    {
        var lines   = new List<string>();
        var sb      = new System.Text.StringBuilder();
        var inQuote = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"') { inQuote = !inQuote; sb.Append(ch); }
            else if ((ch == '\n' || ch == '\r') && !inQuote)
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                lines.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(ch);
        }
        if (sb.Length > 0) lines.Add(sb.ToString());
        return lines;
    }

    private static List<string> SplitCsvRow(string line)
    {
        var fields  = new List<string>();
        var sb      = new System.Text.StringBuilder();
        var inQuote = false;
        var i       = 0;
        while (i < line.Length)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                { sb.Append('"'); i += 2; continue; }
                inQuote = !inQuote;
                i++;
            }
            else if (ch == ',' && !inQuote)
            { fields.Add(sb.ToString()); sb.Clear(); i++; }
            else { sb.Append(ch); i++; }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    // ── Column accessor ───────────────────────────────────────────────────────

    private static string GetColumn(Dictionary<string, string> row, params string[] candidates)
    {
        foreach (var key in candidates)
            if (row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                return val.Trim();
        return string.Empty;
    }

    // ── Name extraction: "Name (https://...)" → "Name" ───────────────────────

    internal static string ExtractName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var idx = raw.IndexOf(" (", StringComparison.Ordinal);
        return idx >= 0 ? raw[..idx].Trim() : raw.Trim();
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ── Difficulty flags: "Heróico, Mítico" → DifficultyFlags ────────────────

    internal static DifficultyFlags ParseDifficultyFlags(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DifficultyFlags.None;
        var result = DifficultyFlags.None;
        foreach (var part in raw.Split(',', '/', ';'))
        {
            var s = part.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            s = new string(s.Where(c =>
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());

            result |= s switch
            {
                "lfr" or "buscador de raid" or "buscador"              => DifficultyFlags.LFR,
                "normal"                                               => DifficultyFlags.Normal,
                "heroic" or "heroico" or "heroico" or "heroïc"        => DifficultyFlags.Heroic,
                "mythic" or "mitico" or "mitico" or "mileg" or "elite" => DifficultyFlags.Mythic,
                _ => DifficultyFlags.None,
            };
        }
        return result;
    }

    // ── Single difficulty: "Mythic" → Difficulty ─────────────────────────────

    internal static Difficulty ParseDifficulty(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Difficulty.Normal;
        var s = Normalize(raw);
        return s switch
        {
            "lfr" or "buscador"    => Difficulty.LFR,
            "normal"               => Difficulty.Normal,
            "heroic" or "heroico"  => Difficulty.Heroic,
            "mythic" or "mitico"   => Difficulty.Mythic,
            _ => Difficulty.Normal,
        };
    }

    // ── Frequency ─────────────────────────────────────────────────────────────

    internal static Frequency ParseFrequency(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Frequency.Weekly;
        return Normalize(raw) switch
        {
            "hourly" or "hora" or "por hora"               => Frequency.Hourly,
            "daily" or "diario" or "diaria"                => Frequency.Daily,
            "weekly" or "semanal"                          => Frequency.Weekly,
            "monthly" or "mensual"                         => Frequency.Monthly,
            _ => Frequency.Weekly,
        };
    }

    // ── TrackingStatus ────────────────────────────────────────────────────────

    internal static TrackingStatus ParseTrackingStatus(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return TrackingStatus.NotStarted;
        return Normalize(raw) switch
        {
            "notstarted" or "not started" or "no iniciado" or "pendiente de iniciar"   => TrackingStatus.NotStarted,
            "pending" or "pendiente"                                                   => TrackingStatus.Pending,
            "inprogress" or "in progress" or "en progreso" or "encurso"               => TrackingStatus.InProgress,
            "lastweek" or "last week" or "ultima semana" or "semana pasada"            => TrackingStatus.LastWeek,
            "finished" or "terminado" or "completado" or "done" or "hecho"             => TrackingStatus.Finished,
            _ => TrackingStatus.NotStarted,
        };
    }

    // ── MotiveFlags: "Mounts,Transmog" → MotiveFlags ─────────────────────────

    internal static MotiveFlags ParseMotiveFlags(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return MotiveFlags.None;
        var result = MotiveFlags.None;
        foreach (var part in raw.Split(',', ';'))
        {
            result |= Normalize(part) switch
            {
                "mounts" or "montura" or "monturas" => MotiveFlags.Mounts,
                "transmog" or "transmogrification"  => MotiveFlags.Transmog,
                "reputation" or "reputacion"        => MotiveFlags.Reputation,
                "anima"                             => MotiveFlags.Anima,
                "achievement" or "logro" or "logros" => MotiveFlags.Achievement,
                _ => MotiveFlags.None,
            };
        }
        return result;
    }

    // ── Normalize: lowercase + strip diacritics ───────────────────────────────

    private static string Normalize(string raw)
    {
        var lower    = raw.Trim().ToLowerInvariant();
        var decomposed = lower.Normalize(System.Text.NormalizationForm.FormD);
        var clean    = new string(decomposed
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        // also collapse whitespace and remove spaces for switch matching
        return Regex.Replace(clean, @"\s+", " ").Trim();
    }

    // ── File finder ───────────────────────────────────────────────────────────

    private static string? FindFile(string dir, string pattern) =>
        Directory
            .EnumerateFiles(dir, "*.csv", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => Path.GetFileName(f).Contains(pattern,
                StringComparison.OrdinalIgnoreCase));
}
