using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIM.Storage.Migrations
{
    [DbContext(typeof(AimDbContext))]
    [Migration("20260601134000_AvatarImagePath")]
    public partial class AvatarImagePath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarImagePath",
                table: "Personalities",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarImagePath",
                table: "Personalities");
        }
    }
}
