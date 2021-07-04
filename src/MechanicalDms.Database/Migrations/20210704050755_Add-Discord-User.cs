using Microsoft.EntityFrameworkCore.Migrations;

namespace MechanicalDms.Database.Migrations
{
    public partial class AddDiscordUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLegitCopy",
                schema: "mechanical_dms",
                table: "MinecraftPlayer",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DiscordUser",
                schema: "mechanical_dms",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentifyNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Element = table.Column<int>(type: "int", nullable: false),
                    IsGuard = table.Column<bool>(type: "bit", nullable: false),
                    MinecraftPlayerUuid = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordUser", x => x.Uid);
                    table.ForeignKey(
                        name: "FK_DiscordUser_MinecraftPlayer_MinecraftPlayerUuid",
                        column: x => x.MinecraftPlayerUuid,
                        principalSchema: "mechanical_dms",
                        principalTable: "MinecraftPlayer",
                        principalColumn: "Uuid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscordUser_MinecraftPlayerUuid",
                schema: "mechanical_dms",
                table: "DiscordUser",
                column: "MinecraftPlayerUuid");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordUser",
                schema: "mechanical_dms");

            migrationBuilder.DropColumn(
                name: "IsLegitCopy",
                schema: "mechanical_dms",
                table: "MinecraftPlayer");
        }
    }
}
