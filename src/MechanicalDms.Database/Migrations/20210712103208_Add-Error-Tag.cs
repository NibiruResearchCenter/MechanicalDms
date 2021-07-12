using Microsoft.EntityFrameworkCore.Migrations;

namespace MechanicalDms.Database.Migrations
{
    public partial class AddErrorTag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SyncError",
                schema: "mechanical_dms",
                table: "KaiheilaUser",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SyncError",
                schema: "mechanical_dms",
                table: "DiscordUser",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SyncError",
                schema: "mechanical_dms",
                table: "KaiheilaUser");

            migrationBuilder.DropColumn(
                name: "SyncError",
                schema: "mechanical_dms",
                table: "DiscordUser");
        }
    }
}
