using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingConnectorsSwitchOnAtAndSwitchOffAtCurrent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SwitchOffAtCurrent",
                table: "OcppChargingStationConnectors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SwitchOnAtCurrent",
                table: "OcppChargingStationConnectors",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SwitchOffAtCurrent",
                table: "OcppChargingStationConnectors");

            migrationBuilder.DropColumn(
                name: "SwitchOnAtCurrent",
                table: "OcppChargingStationConnectors");
        }
    }
}
