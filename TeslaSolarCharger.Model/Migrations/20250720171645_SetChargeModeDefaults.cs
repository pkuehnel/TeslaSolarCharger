using Microsoft.EntityFrameworkCore.Migrations;
using TeslaSolarCharger.Shared.Enums;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class SetChargeModeDefaults : Migration
    {
        private const int AUTO_CHARGE_MODE = (int)ChargeModeV2.Auto;
        private const int MAX_POWER_CHARGE_MODE = (int)ChargeModeV2.MaxPower;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ChargeMode",
                table: "OcppChargingStationConnectors",
                type: "INTEGER",
                nullable: false,
                defaultValue: AUTO_CHARGE_MODE,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ChargeMode",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: AUTO_CHARGE_MODE,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.Sql($"UPDATE Cars SET ChargeMode = {MAX_POWER_CHARGE_MODE}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ChargeMode",
                table: "OcppChargingStationConnectors",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: AUTO_CHARGE_MODE);

            migrationBuilder.AlterColumn<int>(
                name: "ChargeMode",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: AUTO_CHARGE_MODE);
        }
    }
}
