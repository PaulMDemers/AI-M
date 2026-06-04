using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIM.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ConversationSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Conversations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SummaryUpdatedAt",
                table: "Conversations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SummaryUpdatedAt",
                table: "Conversations");
        }
    }
}
