using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddChargerValueLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChargerValueLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IntValue = table.Column<int>(type: "INTEGER", nullable: false),
                    OcppChargingStationConnectorId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargerValueLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargerValueLog_OcppChargingStationConnectors_OcppChargingStationConnectorId",
                        column: x => x.OcppChargingStationConnectorId,
                        principalTable: "OcppChargingStationConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargerValueLog_OcppChargingStationConnectorId",
                table: "ChargerValueLog",
                column: "OcppChargingStationConnectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargerValueLog");
        }
    }
}
