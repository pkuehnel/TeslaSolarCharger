using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChargingProcesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsedGridEnergy = table.Column<decimal>(type: "TEXT", nullable: true),
                    UsedSolarEnergy = table.Column<decimal>(type: "TEXT", nullable: true),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: true),
                    CarId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargingProcesses_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChargingDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimeStamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SolarPower = table.Column<int>(type: "INTEGER", nullable: false),
                    GridPower = table.Column<int>(type: "INTEGER", nullable: false),
                    ChargingProcessId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargingDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargingDetails_ChargingProcesses_ChargingProcessId",
                        column: x => x.ChargingProcessId,
                        principalTable: "ChargingProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChargingDetails_ChargingProcessId",
                table: "ChargingDetails",
                column: "ChargingProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ChargingProcesses_CarId",
                table: "ChargingProcesses",
                column: "CarId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChargingDetails");

            migrationBuilder.DropTable(
                name: "ChargingProcesses");
        }
    }
}
