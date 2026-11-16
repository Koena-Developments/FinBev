using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiptReminders",
                columns: table => new
                {
                    CashRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeRemindedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ManagerRemindedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptReminders", x => x.CashRequestId);
                    table.ForeignKey(
                        name: "FK_ReceiptReminders_CashRequests_CashRequestId",
                        column: x => x.CashRequestId,
                        principalTable: "CashRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptReminders");
        }
    }
}
