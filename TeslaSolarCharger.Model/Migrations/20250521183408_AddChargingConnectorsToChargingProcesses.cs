using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingConnectorsToChargingProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargingProcesses_Cars_CarId",
                table: "ChargingProcesses");

            migrationBuilder.AlterColumn<int>(
                name: "CarId",
                table: "ChargingProcesses",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "OcppChargingStationConnectorId",
                table: "ChargingProcesses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargingProcesses_OcppChargingStationConnectorId",
                table: "ChargingProcesses",
                column: "OcppChargingStationConnectorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargingProcesses_Cars_CarId",
                table: "ChargingProcesses",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChargingProcesses_OcppChargingStationConnectors_OcppChargingStationConnectorId",
                table: "ChargingProcesses",
                column: "OcppChargingStationConnectorId",
                principalTable: "OcppChargingStationConnectors",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargingProcesses_Cars_CarId",
                table: "ChargingProcesses");

            migrationBuilder.DropForeignKey(
                name: "FK_ChargingProcesses_OcppChargingStationConnectors_OcppChargingStationConnectorId",
                table: "ChargingProcesses");

            migrationBuilder.DropIndex(
                name: "IX_ChargingProcesses_OcppChargingStationConnectorId",
                table: "ChargingProcesses");

            migrationBuilder.DropColumn(
                name: "OcppChargingStationConnectorId",
                table: "ChargingProcesses");

            migrationBuilder.AlterColumn<int>(
                name: "CarId",
                table: "ChargingProcesses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChargingProcesses_Cars_CarId",
                table: "ChargingProcesses",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
