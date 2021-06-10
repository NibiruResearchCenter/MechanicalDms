using Microsoft.EntityFrameworkCore.Migrations;

namespace MechanicalDms.Database.Migrations
{
    public partial class Initialize : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mechanical_dms");

            migrationBuilder.CreateTable(
                name: "BilibiliUser",
                schema: "mechanical_dms",
                columns: table => new
                {
                    Uid = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GuardLevel = table.Column<int>(type: "int", nullable: false),
                    ExpireTime = table.Column<long>(type: "bigint", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BilibiliUser", x => x.Uid);
                });

            migrationBuilder.CreateTable(
                name: "MinecraftPlayer",
                schema: "mechanical_dms",
                columns: table => new
                {
                    Uuid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinecraftPlayer", x => x.Uuid);
                });

            migrationBuilder.CreateTable(
                name: "KaiheilaUser",
                schema: "mechanical_dms",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentifyNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Roles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BilibiliUserUid = table.Column<long>(type: "bigint", nullable: true),
                    MinecraftPlayerUuid = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KaiheilaUser", x => x.Uid);
                    table.ForeignKey(
                        name: "FK_KaiheilaUser_BilibiliUser_BilibiliUserUid",
                        column: x => x.BilibiliUserUid,
                        principalSchema: "mechanical_dms",
                        principalTable: "BilibiliUser",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KaiheilaUser_MinecraftPlayer_MinecraftPlayerUuid",
                        column: x => x.MinecraftPlayerUuid,
                        principalSchema: "mechanical_dms",
                        principalTable: "MinecraftPlayer",
                        principalColumn: "Uuid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KaiheilaUser_BilibiliUserUid",
                schema: "mechanical_dms",
                table: "KaiheilaUser",
                column: "BilibiliUserUid");

            migrationBuilder.CreateIndex(
                name: "IX_KaiheilaUser_MinecraftPlayerUuid",
                schema: "mechanical_dms",
                table: "KaiheilaUser",
                column: "MinecraftPlayerUuid");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KaiheilaUser",
                schema: "mechanical_dms");

            migrationBuilder.DropTable(
                name: "BilibiliUser",
                schema: "mechanical_dms");

            migrationBuilder.DropTable(
                name: "MinecraftPlayer",
                schema: "mechanical_dms");
        }
    }
}
