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
            if (!ctx.IsAdmin()) return Results.Forbid();

            var characters = await db.Characters
                .Include(c => c.Warband)
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
            if (!ctx.IsAdmin()) return Results.Forbid();

            var contents = await db.Contents
                .Include(c => c.Motives)
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
            if (!ctx.IsAdmin()) return Results.Forbid();

            var trackings = await db.Trackings
                .Include(t => t.Character)
                .Include(t => t.Content)
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
            if (!ctx.IsAdmin()) return Results.Forbid();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var rows = CsvImportHelper.ParseCsvText(text);
            var adminUser = await db.Users.FirstOrDefaultAsync(u => u.IsAdmin);
            if (adminUser == null) return Results.Problem("No admin user found.");

            var imported = 0;
            foreach (var row in rows)
            {
                var name = GetCol(row, "Name", "Nombre");
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (await db.Characters.AnyAsync(c => c.Name == name)) continue;

                var warbandName = GetColNullable(row, "Warband");
                Guid? warbandId = null;
                if (warbandName != null)
                {
                    var warband = await db.Warbands.FirstOrDefaultAsync(w => w.OwnerUserId == adminUser.Id && w.Name == warbandName);
                    if (warband == null)
                    {
                        warband = new Warband { Name = warbandName, OwnerUserId = adminUser.Id };
                        db.Warbands.Add(warband);
                        await db.SaveChangesAsync();
                    }
                    warbandId = warband.Id;
                }

                int? level = int.TryParse(GetCol(row, "Level", "Nivel"), out var lv) ? lv : null;
                db.Characters.Add(new Character
                {
                    Name = name,
                    Class = GetCol(row, "Class", "Clase") is { Length: > 0 } cls ? cls : "Unknown",
                    Race = GetColNullable(row, "Race", "Raza"),
                    Level = level,
                    Covenant = GetColNullable(row, "Covenant", "Pacto"),
                    WarbandId = warbandId,
                    OwnerUserId = adminUser.Id,
                });
                imported++;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { imported });
        }).DisableAntiforgery().WithName("ImportCharacters").WithSummary("Admin: import characters from CSV");

        group.MapPost("/import/content", async (HttpContext ctx, AppDbContext db, HttpRequest req) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var rows = CsvImportHelper.ParseCsvText(text);
            var adminUser = await db.Users.FirstOrDefaultAsync(u => u.IsAdmin);
            if (adminUser == null) return Results.Problem("No admin user found.");

            var imported = 0;
            foreach (var row in rows)
            {
                var name = GetCol(row, "Name", "Nombre");
                var expansion = GetCol(row, "Expansion") is { Length: > 0 } exp ? exp : "Unknown";
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (await db.Contents.AnyAsync(c => c.Name == name && c.Expansion == expansion)) continue;

                var motiveStr = GetCol(row, "Motives", "Motivos");
                var motives = new List<UserMotive>();
                foreach (var part in motiveStr.Split('|', ',', ';'))
                {
                    var mn = part.Trim();
                    if (string.IsNullOrWhiteSpace(mn)) continue;
                    var m = await db.UserMotives.FirstOrDefaultAsync(x => x.OwnerUserId == adminUser.Id && x.Name == mn);
                    if (m == null)
                    {
                        m = new UserMotive { Name = mn, OwnerUserId = adminUser.Id };
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
                    AllowedDifficulties = (int)CsvImportHelper.ParseDifficultyFlags(GetCol(row, "Difficulties", "Dificultades", "AllowedDifficulties")),
                    Motives = motives,
                });
                imported++;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { imported });
        }).DisableAntiforgery().WithName("ImportContent").WithSummary("Admin: import content from CSV");

        group.MapPost("/import/progress", async (HttpContext ctx, AppDbContext db, HttpRequest req) =>
        {
            if (!ctx.IsAdmin()) return Results.Forbid();

            var text = await ReadBody(req);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "No CSV content provided." });

            var rows = CsvImportHelper.ParseCsvText(text);
            var imported = 0;
            var skipped = 0;

            foreach (var row in rows)
            {
                var charName = GetCol(row, "Character", "Personaje");
                var contentName = GetCol(row, "Content", "Contenido");
                if (string.IsNullOrWhiteSpace(charName) || string.IsNullOrWhiteSpace(contentName))
                {
                    skipped++;
                    continue;
                }

                var character = await db.Characters.FirstOrDefaultAsync(c => c.Name == charName);
                var content = await db.Contents.FirstOrDefaultAsync(c => c.Name == contentName);
                if (character == null || content == null) { skipped++; continue; }

                var difficulty = CsvImportHelper.ParseDifficulty(GetCol(row, "Difficulty", "Dificultad"));
                if (await db.Trackings.AnyAsync(t => t.CharacterId == character.Id && t.ContentId == content.Id && t.Difficulty == difficulty))
                {
                    skipped++;
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
            return Results.Ok(new { imported, skipped });
        }).DisableAntiforgery().WithName("ImportProgress").WithSummary("Admin: import progress trackings from CSV");
    }

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
