using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIM.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ConversationGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Conversations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ConversationGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonalityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationGroups", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO ConversationGroups (Id, PersonalityId, Title, CreatedAt, ArchivedAt)
                SELECT
                    lower(hex(randomblob(4))) || '-' ||
                    lower(hex(randomblob(2))) || '-' ||
                    lower(hex(randomblob(2))) || '-' ||
                    lower(hex(randomblob(2))) || '-' ||
                    lower(hex(randomblob(6))),
                    PersonalityId,
                    'General',
                    MIN(CreatedAt),
                    NULL
                FROM Conversations
                GROUP BY PersonalityId
                """);

            migrationBuilder.Sql(
                """
                UPDATE Conversations
                SET GroupId = (
                    SELECT ConversationGroups.Id
                    FROM ConversationGroups
                    WHERE ConversationGroups.PersonalityId = Conversations.PersonalityId
                    ORDER BY ConversationGroups.CreatedAt
                    LIMIT 1
                )
                WHERE GroupId = '00000000-0000-0000-0000-000000000000'
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_GroupId",
                table: "Conversations",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationGroups_PersonalityId",
                table: "ConversationGroups",
                column: "PersonalityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationGroups");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_GroupId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Conversations");
        }
    }
}
