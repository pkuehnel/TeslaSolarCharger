using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddWsToEnergyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MeasuredEnergy",
                table: "MeterValues",
                newName: "MeasuredEnergyWs");

            migrationBuilder.RenameColumn(
                name: "EstimatedEnergy",
                table: "MeterValues",
                newName: "EstimatedEnergyWs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MeasuredEnergyWs",
                table: "MeterValues",
                newName: "MeasuredEnergy");

            migrationBuilder.RenameColumn(
                name: "EstimatedEnergyWs",
                table: "MeterValues",
                newName: "EstimatedEnergy");
        }
    }
}
