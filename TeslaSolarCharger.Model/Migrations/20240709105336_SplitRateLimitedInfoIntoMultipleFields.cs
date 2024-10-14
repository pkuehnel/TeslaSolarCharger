using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class SplitRateLimitedInfoIntoMultipleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RateLimitedUntil",
                table: "Cars",
                newName: "WakeUpRateLimitedUntil");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.RenameColumn(
                name: "WakeUpRateLimitedUntil",
                table: "Cars",
                newName: "RateLimitedUntil");
        }
    }
}
