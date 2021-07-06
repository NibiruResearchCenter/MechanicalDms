using Microsoft.EntityFrameworkCore.Migrations;

namespace MechanicalDms.Database.Migrations
{
    public partial class AddCrackedPlayer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MDAuthCrackedPlayer",
                schema: "mechanical_dms",
                columns: table => new
                {
                    Uuid = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MDAuthCrackedPlayer", x => x.Uuid);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MDAuthCrackedPlayer",
                schema: "mechanical_dms");
        }
    }
}
