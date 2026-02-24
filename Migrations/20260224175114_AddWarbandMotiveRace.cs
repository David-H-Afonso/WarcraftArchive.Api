using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarcraftArchive.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWarbandMotiveRace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Motives",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "Warband",
                table: "Characters");

            migrationBuilder.AddColumn<string>(
                name: "Race",
                table: "Characters",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarbandId",
                table: "Characters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserMotives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMotives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMotives_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Warbands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warbands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warbands_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentUserMotives",
                columns: table => new
                {
                    ContentsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MotivesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentUserMotives", x => new { x.ContentsId, x.MotivesId });
                    table.ForeignKey(
                        name: "FK_ContentUserMotives_Contents_ContentsId",
                        column: x => x.ContentsId,
                        principalTable: "Contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentUserMotives_UserMotives_MotivesId",
                        column: x => x.MotivesId,
                        principalTable: "UserMotives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_WarbandId",
                table: "Characters",
                column: "WarbandId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentUserMotives_MotivesId",
                table: "ContentUserMotives",
                column: "MotivesId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMotives_OwnerUserId_Name",
                table: "UserMotives",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warbands_OwnerUserId_Name",
                table: "Warbands",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Warbands_WarbandId",
                table: "Characters",
                column: "WarbandId",
                principalTable: "Warbands",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Warbands_WarbandId",
                table: "Characters");

            migrationBuilder.DropTable(
                name: "ContentUserMotives");

            migrationBuilder.DropTable(
                name: "Warbands");

            migrationBuilder.DropTable(
                name: "UserMotives");

            migrationBuilder.DropIndex(
                name: "IX_Characters_WarbandId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Race",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "WarbandId",
                table: "Characters");

            migrationBuilder.AddColumn<int>(
                name: "Motives",
                table: "Contents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Warband",
                table: "Characters",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }
    }
}
