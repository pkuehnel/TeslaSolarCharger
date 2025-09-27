using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingStationConnectorsAllowedCars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChargingStationConnectorAllowedCars",
                columns: table => new
                {
                    CarId = table.Column<int>(type: "INTEGER", nullable: false),
                    OcppChargingStationConnectorId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingStationConnectorAllowedCars", x => new { x.CarId, x.OcppChargingStationConnectorId });
                    table.ForeignKey(
                        name: "FK_ChargingStationConnectorAllowedCars_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChargingStationConnectorAllowedCars_OcppChargingStationConnectors_OcppChargingStationConnectorId",
                        column: x => x.OcppChargingStationConnectorId,
                        principalTable: "OcppChargingStationConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingStationConnectorAllowedCars_OcppChargingStationConnectorId",
                table: "ChargingStationConnectorAllowedCars",
                column: "OcppChargingStationConnectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargingStationConnectorAllowedCars");
        }
    }
}
