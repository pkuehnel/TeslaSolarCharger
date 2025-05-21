using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddMorePropertiesToChargingConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoSwitchBetween1And3PhasesEnabled",
                table: "OcppChargingStations");

            migrationBuilder.DropColumn(
                name: "MaxCurrent",
                table: "OcppChargingStations");

            migrationBuilder.AddColumn<bool>(
                name: "AutoSwitchBetween1And3PhasesEnabled",
                table: "OcppChargingStationConnectors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxCurrent",
                table: "OcppChargingStationConnectors",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinCurrent",
                table: "OcppChargingStationConnectors",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoSwitchBetween1And3PhasesEnabled",
                table: "OcppChargingStationConnectors");

            migrationBuilder.DropColumn(
                name: "MaxCurrent",
                table: "OcppChargingStationConnectors");

            migrationBuilder.DropColumn(
                name: "MinCurrent",
                table: "OcppChargingStationConnectors");

            migrationBuilder.AddColumn<bool>(
                name: "AutoSwitchBetween1And3PhasesEnabled",
                table: "OcppChargingStations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxCurrent",
                table: "OcppChargingStations",
                type: "INTEGER",
                nullable: true);
        }
    }
}
