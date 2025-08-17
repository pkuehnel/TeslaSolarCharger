using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnnecessaryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedCarStates");

            migrationBuilder.DropIndex(
                name: "IX_CarValueLogs_CarId",
                table: "CarValueLogs");

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
                name: "IgnoreLatestTimeToReachSocDate",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "IgnoreLatestTimeToReachSocDateOnWeekend",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "LatestTimeToReachSoC",
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

            migrationBuilder.CreateIndex(
                name: "IX_CarValueLogs_CarId_Type_Timestamp",
                table: "CarValueLogs",
                columns: new[] { "CarId", "Type", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CarValueLogs_CarId_Type_Timestamp",
                table: "CarValueLogs");

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
                name: "IgnoreLatestTimeToReachSocDate",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreLatestTimeToReachSocDateOnWeekend",
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

            migrationBuilder.CreateTable(
                name: "CachedCarStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CarId = table.Column<int>(type: "INTEGER", nullable: false),
                    CarStateJson = table.Column<string>(type: "TEXT", nullable: true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedCarStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarValueLogs_CarId",
                table: "CarValueLogs",
                column: "CarId");
        }
    }
}
