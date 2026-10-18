using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddUsableEnergyLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UsableEnergyLearnedSince",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UsableEnergySamples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CarId = table.Column<int>(type: "INTEGER", nullable: true),
                    CarVin = table.Column<string>(type: "TEXT", nullable: true),
                    ChargingProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    StartTime = table.Column<long>(type: "INTEGER", nullable: false),
                    EndTime = table.Column<long>(type: "INTEGER", nullable: false),
                    StartSoc = table.Column<int>(type: "INTEGER", nullable: false),
                    EndSoc = table.Column<int>(type: "INTEGER", nullable: false),
                    EnergyWh = table.Column<int>(type: "INTEGER", nullable: false),
                    RejectionReason = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsableEnergySamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsableEnergySamples_Cars_CarId",
                        column: x => x.CarId,
                        principalTable: "Cars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsableEnergySamples_CarId",
                table: "UsableEnergySamples",
                column: "CarId");

            migrationBuilder.CreateIndex(
                name: "IX_UsableEnergySamples_ChargingProcessId",
                table: "UsableEnergySamples",
                column: "ChargingProcessId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsableEnergySamples_Kind_CarId_EndTime",
                table: "UsableEnergySamples",
                columns: new[] { "Kind", "CarId", "EndTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsableEnergySamples");

            migrationBuilder.DropColumn(
                name: "UsableEnergyLearnedSince",
                table: "Cars");
        }
    }
}
