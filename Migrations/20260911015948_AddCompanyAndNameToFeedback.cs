using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaqCms.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyAndNameToFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "ArticleFeedbacks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ArticleFeedbacks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 9, 11, 1, 59, 48, 124, DateTimeKind.Utc).AddTicks(9303));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 9, 11, 1, 59, 48, 124, DateTimeKind.Utc).AddTicks(9308));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 9, 11, 1, 59, 48, 124, DateTimeKind.Utc).AddTicks(9311));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Company",
                table: "ArticleFeedbacks");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ArticleFeedbacks");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 9, 10, 9, 25, 53, 800, DateTimeKind.Utc).AddTicks(9684));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 9, 10, 9, 25, 53, 800, DateTimeKind.Utc).AddTicks(9691));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 9, 10, 9, 25, 53, 800, DateTimeKind.Utc).AddTicks(9693));
        }
    }
}
