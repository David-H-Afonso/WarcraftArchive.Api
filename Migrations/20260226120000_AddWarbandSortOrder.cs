using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarcraftArchive.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWarbandSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Warbands",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Warbands");
        }
    }
}
