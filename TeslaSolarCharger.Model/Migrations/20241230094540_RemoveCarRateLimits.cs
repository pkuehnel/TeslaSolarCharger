using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCarRateLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargingCommandsRateLimitedUntil",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "CommandsRateLimitedUntil",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "VehicleDataRateLimitedUntil",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "VehicleRateLimitedUntil",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "WakeUpRateLimitedUntil",
                table: "Cars");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ChargingCommandsRateLimitedUntil",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommandsRateLimitedUntil",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VehicleDataRateLimitedUntil",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VehicleRateLimitedUntil",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WakeUpRateLimitedUntil",
                table: "Cars",
                type: "TEXT",
                nullable: true);
        }
    }
}
