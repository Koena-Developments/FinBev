using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixApproverAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApproverAvailabilities_AspNetUsers_UserId1",
                table: "ApproverAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_ApproverAvailabilities_UserId1",
                table: "ApproverAvailabilities");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "ApproverAvailabilities");

            migrationBuilder.AddForeignKey(
                name: "FK_ApproverAvailabilities_AspNetUsers_UserId",
                table: "ApproverAvailabilities",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApproverAvailabilities_AspNetUsers_UserId",
                table: "ApproverAvailabilities");

            migrationBuilder.AddColumn<string>(
                name: "UserId1",
                table: "ApproverAvailabilities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ApproverAvailabilities_UserId1",
                table: "ApproverAvailabilities",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ApproverAvailabilities_AspNetUsers_UserId1",
                table: "ApproverAvailabilities",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
