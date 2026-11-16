using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiStageApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovalTasks_CashRequestId",
                table: "ApprovalTasks");

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "ApprovalTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalTasks_CashRequestId",
                table: "ApprovalTasks",
                column: "CashRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovalTasks_CashRequestId",
                table: "ApprovalTasks");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "ApprovalTasks");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalTasks_CashRequestId",
                table: "ApprovalTasks",
                column: "CashRequestId",
                unique: true);
        }
    }
}
