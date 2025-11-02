using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class UseMillisecondsTimestampChargingConnectorValueLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"UPDATE OcppChargingStationConnectorValueLogs
SET Timestamp = ROUND((julianday(Timestamp) - 2440587.5) * 86400000)
WHERE typeof(Timestamp) = 'text';");

            migrationBuilder.AlterColumn<long>(
                name: "Timestamp",
                table: "OcppChargingStationConnectorValueLogs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"UPDATE OcppChargingStationConnectorValueLogs
SET Timestamp = STRFTIME('%Y-%m-%dT%H:%M:%fZ', Timestamp / 1000.0, 'unixepoch')
WHERE typeof(Timestamp) = 'integer';");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Timestamp",
                table: "OcppChargingStationConnectorValueLogs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");
        }
    }
}
