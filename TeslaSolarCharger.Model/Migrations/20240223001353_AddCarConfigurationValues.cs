using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddCarConfigurationValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChargeMode",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChargingPriority",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreLatestTimeToReachSocDate",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestTimeToReachSoC",
                table: "Cars",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "MaximumAmpere",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAmpere",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumSoc",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShouldBeManaged",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShouldSetChargeStartTimes",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsableEnergy",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Vin",
                table: "Cars",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargeMode",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ChargingPriority",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "IgnoreLatestTimeToReachSocDate",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "LatestTimeToReachSoC",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "MaximumAmpere",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "MinimumAmpere",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "MinimumSoc",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ShouldBeManaged",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ShouldSetChargeStartTimes",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "UsableEnergy",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Vin",
                table: "Cars");
        }
    }
}
