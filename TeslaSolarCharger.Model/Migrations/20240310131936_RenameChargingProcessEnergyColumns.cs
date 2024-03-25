using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class RenameChargingProcessEnergyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UsedSolarEnergy",
                table: "ChargingProcesses",
                newName: "UsedSolarEnergyKwh");

            migrationBuilder.RenameColumn(
                name: "UsedGridEnergy",
                table: "ChargingProcesses",
                newName: "UsedGridEnergyKwh");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UsedSolarEnergyKwh",
                table: "ChargingProcesses",
                newName: "UsedSolarEnergy");

            migrationBuilder.RenameColumn(
                name: "UsedGridEnergyKwh",
                table: "ChargingProcesses",
                newName: "UsedGridEnergy");
        }
    }
}
