using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PettyCash.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPettyCashFormSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ClaimantSignaturePng",
                table: "CashRequests",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimantSignedAtUtc",
                table: "CashRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinanceFormReleasedAtUtc",
                table: "CashRequests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimantSignaturePng",
                table: "CashRequests");

            migrationBuilder.DropColumn(
                name: "ClaimantSignedAtUtc",
                table: "CashRequests");

            migrationBuilder.DropColumn(
                name: "FinanceFormReleasedAtUtc",
                table: "CashRequests");
        }
    }
}
