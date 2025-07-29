using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class RemoveChargeModeFromCarAndRenameOnChargingConnector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargeMode",
                table: "Cars");

            migrationBuilder.RenameColumn(
                name: "ChargeModeV2",
                table: "OcppChargingStationConnectors",
                newName: "ChargeMode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChargeMode",
                table: "OcppChargingStationConnectors",
                newName: "ChargeModeV2");

            migrationBuilder.AddColumn<int>(
                name: "ChargeMode",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
