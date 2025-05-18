using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumerMeterValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumerMeterValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    MeasuredPowerW = table.Column<int>(type: "INTEGER", nullable: true),
                    MeasuredEnergyWs = table.Column<long>(type: "INTEGER", nullable: true),
                    EstimatedPowerW = table.Column<int>(type: "INTEGER", nullable: true),
                    EstimatedEnergyWs = table.Column<long>(type: "INTEGER", nullable: true),
                    OcppChargingStationConnectorId = table.Column<int>(type: "INTEGER", nullable: true),
                    CarId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumerMeterValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumerMeterValues_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConsumerMeterValues_OcppChargingStationConnectors_OcppChargingStationConnectorId",
                        column: x => x.OcppChargingStationConnectorId,
                        principalTable: "OcppChargingStationConnectors",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerMeterValues_CarId",
                table: "ConsumerMeterValues",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumerMeterValues_OcppChargingStationConnectorId",
                table: "ConsumerMeterValues",
                column: "OcppChargingStationConnectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumerMeterValues");
        }
    }
}
