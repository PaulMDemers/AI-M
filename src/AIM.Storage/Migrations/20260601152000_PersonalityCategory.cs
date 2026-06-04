using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIM.Storage.Migrations
{
    [DbContext(typeof(AimDbContext))]
    [Migration("20260601152000_PersonalityCategory")]
    public partial class PersonalityCategory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Personalities",
                type: "TEXT",
                nullable: false,
                defaultValue: "My Contacts");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Personalities");
        }
    }
}
