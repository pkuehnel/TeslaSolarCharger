using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class CachableCallLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargeStartCalls",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChargeStopCalls",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherCommandCalls",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetChargingAmpsCall",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleCalls",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleDataCalls",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WakeUpCalls",
                table: "Cars",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargeStartCalls",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ChargeStopCalls",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "OtherCommandCalls",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "SetChargingAmpsCall",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "VehicleCalls",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "VehicleDataCalls",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "WakeUpCalls",
                table: "Cars");
        }
    }
}
