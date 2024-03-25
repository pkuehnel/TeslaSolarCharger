using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddCarStatesToCarsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TeslaMateCarId",
                table: "Cars",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "ChargerActualCurrent",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargerPhases",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargerPilotCurrent",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargerRequestedCurrent",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargerVoltage",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ClimateOn",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Cars",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Cars",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PluggedIn",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoC",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SocLimit",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "Cars",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargerActualCurrent",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ChargerPhases",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ChargerPilotCurrent",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ChargerRequestedCurrent",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ChargerVoltage",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ClimateOn",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "PluggedIn",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "SoC",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "SocLimit",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Cars");

            migrationBuilder.AlterColumn<int>(
                name: "TeslaMateCarId",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
