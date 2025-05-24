using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarChargingSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetSoc = table.Column<int>(type: "INTEGER", nullable: false),
                    NextOccurrence = table.Column<long>(type: "INTEGER", nullable: false),
                    RepeatOnMondays = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepeatOnTuesdays = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepeatOnWednesdays = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepeatOnThursdays = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepeatOnFridays = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepeatOnSaturdays = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepeatOnSundays = table.Column<bool>(type: "INTEGER", nullable: false),
                    CarId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarChargingSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarChargingSchedules_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarChargingSchedules_CarId",
                table: "CarChargingSchedules",
                column: "CarId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarChargingSchedules");
        }
    }
}
