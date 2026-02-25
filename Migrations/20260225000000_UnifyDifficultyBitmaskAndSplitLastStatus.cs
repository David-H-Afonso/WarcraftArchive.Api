using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarcraftArchive.Api.Migrations
{
    /// <inheritdoc />
    public partial class UnifyDifficultyBitmaskAndSplitLastStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Task 3: Convert Difficulty enum (0-3) → DifficultyFlags single-bit (1,2,4,8) ──
            // Old: LFR=0, Normal=1, Heroic=2, Mythic=3
            // New: LFR=1, Normal=2, Heroic=4, Mythic=8
            migrationBuilder.Sql(@"
                UPDATE Trackings SET Difficulty =
                    CASE Difficulty
                        WHEN 0 THEN 1
                        WHEN 1 THEN 2
                        WHEN 2 THEN 4
                        WHEN 3 THEN 8
                        ELSE Difficulty
                    END;
            ");

            // ── Task 5: Split TrackingStatus.LastWeek(3) into LastDay(3) / LastWeek(4) ────────
            // Also shift Finished from 4 → 5.
            // Order matters: bump Finished first (4→5) to avoid collision with new LastWeek=4.
            migrationBuilder.Sql("UPDATE Trackings SET Status = 5 WHERE Status = 4;");

            // Old LastWeek=3 where Frequency ≠ Daily(1) → new LastWeek=4
            migrationBuilder.Sql("UPDATE Trackings SET Status = 4 WHERE Status = 3 AND Frequency != 1;");

            // Old LastWeek=3 where Frequency = Daily(1) → stays as LastDay=3 (no change needed)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: Finished 5→4
            migrationBuilder.Sql("UPDATE Trackings SET Status = 4 WHERE Status = 5;");

            // Reverse: LastWeek 4→3
            migrationBuilder.Sql("UPDATE Trackings SET Status = 3 WHERE Status = 4;");

            // Reverse: Difficulty flags → enum values
            migrationBuilder.Sql(@"
                UPDATE Trackings SET Difficulty =
                    CASE Difficulty
                        WHEN 1 THEN 0
                        WHEN 2 THEN 1
                        WHEN 4 THEN 2
                        WHEN 8 THEN 3
                        ELSE Difficulty
                    END;
            ");
        }
    }
}
