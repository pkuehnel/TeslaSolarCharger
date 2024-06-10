using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeBatteryPowerToCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UsedHomeBatteryEnergyKwh",
                table: "ChargingProcesses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeBatteryPower",
                table: "ChargingDetails",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsedHomeBatteryEnergyKwh",
                table: "ChargingProcesses");

            migrationBuilder.DropColumn(
                name: "HomeBatteryPower",
                table: "ChargingDetails");
        }
    }
}
