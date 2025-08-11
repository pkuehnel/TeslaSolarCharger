using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddSolarValuesToMeterValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EstimatedGridEnergyWs",
                table: "MeterValues",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EstimatedHomeBatteryEnergyWs",
                table: "MeterValues",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeasuredGridPower",
                table: "MeterValues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MeasuredHomeBatteryPower",
                table: "MeterValues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedGridEnergyWs",
                table: "MeterValues");

            migrationBuilder.DropColumn(
                name: "EstimatedHomeBatteryEnergyWs",
                table: "MeterValues");

            migrationBuilder.DropColumn(
                name: "MeasuredGridPower",
                table: "MeterValues");

            migrationBuilder.DropColumn(
                name: "MeasuredHomeBatteryPower",
                table: "MeterValues");
        }
    }
}
