using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingStationConnectorIdToMeterValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeterValues_CarId_MeterValueKind_Timestamp",
                table: "MeterValues");

            migrationBuilder.AddColumn<int>(
                name: "ChargingConnectorId",
                table: "MeterValues",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterValues_CarId_MeterValueKind_Timestamp",
                table: "MeterValues",
                columns: new[] { "CarId", "ChargingConnectorId", "MeterValueKind", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MeterValues_ChargingConnectorId",
                table: "MeterValues",
                column: "ChargingConnectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_MeterValues_OcppChargingStationConnectors_ChargingConnectorId",
                table: "MeterValues",
                column: "ChargingConnectorId",
                principalTable: "OcppChargingStationConnectors",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeterValues_OcppChargingStationConnectors_ChargingConnectorId",
                table: "MeterValues");

            migrationBuilder.DropIndex(
                name: "IX_MeterValues_CarId_MeterValueKind_Timestamp",
                table: "MeterValues");

            migrationBuilder.DropIndex(
                name: "IX_MeterValues_ChargingConnectorId",
                table: "MeterValues");

            migrationBuilder.DropColumn(
                name: "ChargingConnectorId",
                table: "MeterValues");

            migrationBuilder.CreateIndex(
                name: "IX_MeterValues_CarId_MeterValueKind_Timestamp",
                table: "MeterValues",
                columns: new[] { "CarId", "MeterValueKind", "Timestamp" });
        }
    }
}
