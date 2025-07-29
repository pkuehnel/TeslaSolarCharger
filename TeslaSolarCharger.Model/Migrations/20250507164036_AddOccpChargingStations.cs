using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddOccpChargingStations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OcppChargingStations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChargepointId = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigurationVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CanSwitchBetween1And3Phases = table.Column<bool>(type: "INTEGER", nullable: true),
                    MaxCurrent = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcppChargingStations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OcppChargingStationConnectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectorId = table.Column<int>(type: "INTEGER", nullable: false),
                    OcppChargingStationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcppChargingStationConnectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcppChargingStationConnectors_OcppChargingStations_OcppChargingStationId",
                        column: x => x.OcppChargingStationId,
                        principalTable: "OcppChargingStations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcppChargingStationConnectors_OcppChargingStationId",
                table: "OcppChargingStationConnectors",
                column: "OcppChargingStationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcppChargingStationConnectors");

            migrationBuilder.DropTable(
                name: "OcppChargingStations");
        }
    }
}
