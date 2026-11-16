using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixApprovalTaskForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalTasks_AspNetUsers_AssignedApproverId",
                table: "ApprovalTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalTasks_AspNetUsers_OnBehalfOfId",
                table: "ApprovalTasks");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalTasks_AssignedApproverId",
                table: "ApprovalTasks");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalTasks_OnBehalfOfId",
                table: "ApprovalTasks");

            migrationBuilder.DropColumn(
                name: "AssignedApproverId",
                table: "ApprovalTasks");

            migrationBuilder.DropColumn(
                name: "OnBehalfOfId",
                table: "ApprovalTasks");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalTasks_AssignedApproverUserId",
                table: "ApprovalTasks",
                column: "AssignedApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalTasks_OnBehalfOfUserId",
                table: "ApprovalTasks",
                column: "OnBehalfOfUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalTasks_AspNetUsers_AssignedApproverUserId",
                table: "ApprovalTasks",
                column: "AssignedApproverUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalTasks_AspNetUsers_OnBehalfOfUserId",
                table: "ApprovalTasks",
                column: "OnBehalfOfUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalTasks_AspNetUsers_AssignedApproverUserId",
                table: "ApprovalTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalTasks_AspNetUsers_OnBehalfOfUserId",
                table: "ApprovalTasks");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalTasks_AssignedApproverUserId",
                table: "ApprovalTasks");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalTasks_OnBehalfOfUserId",
                table: "ApprovalTasks");

            migrationBuilder.AddColumn<string>(
                name: "AssignedApproverId",
                table: "ApprovalTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnBehalfOfId",
                table: "ApprovalTasks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalTasks_AssignedApproverId",
                table: "ApprovalTasks",
                column: "AssignedApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalTasks_OnBehalfOfId",
                table: "ApprovalTasks",
                column: "OnBehalfOfId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalTasks_AspNetUsers_AssignedApproverId",
                table: "ApprovalTasks",
                column: "AssignedApproverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalTasks_AspNetUsers_OnBehalfOfId",
                table: "ApprovalTasks",
                column: "OnBehalfOfId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
