using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarcraftArchive.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerUserIdToContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Contents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contents_OwnerUserId",
                table: "Contents",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contents_Users_OwnerUserId",
                table: "Contents",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contents_Users_OwnerUserId",
                table: "Contents");

            migrationBuilder.DropIndex(
                name: "IX_Contents_OwnerUserId",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Contents");
        }
    }
}
