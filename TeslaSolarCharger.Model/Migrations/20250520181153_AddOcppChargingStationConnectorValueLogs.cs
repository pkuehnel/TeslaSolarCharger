using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddOcppChargingStationConnectorValueLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OcppChargingStationConnectorValueLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    BooleanValue = table.Column<bool>(type: "INTEGER", nullable: false),
                    OcppChargingStationConnectorId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcppChargingStationConnectorValueLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcppChargingStationConnectorValueLogs_OcppChargingStationConnectors_OcppChargingStationConnectorId",
                        column: x => x.OcppChargingStationConnectorId,
                        principalTable: "OcppChargingStationConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcppChargingStationConnectorValueLogs_OcppChargingStationConnectorId",
                table: "OcppChargingStationConnectorValueLogs",
                column: "OcppChargingStationConnectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcppChargingStationConnectorValueLogs");
        }
    }
}
