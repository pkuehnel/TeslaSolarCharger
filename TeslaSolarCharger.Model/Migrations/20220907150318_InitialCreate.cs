using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChargePrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ValidSince = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SolarPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    GridPrice = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargePrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HandledCharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChargingProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    CarId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsedGridEnergy = table.Column<int>(type: "INTEGER", nullable: true),
                    UsedSolarEnergy = table.Column<int>(type: "INTEGER", nullable: true),
                    CalculatedPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    ChargePriceId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandledCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandledCharges_ChargePrices_ChargePriceId",
                        column: x => x.ChargePriceId,
                        principalTable: "ChargePrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PowerDistributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimeStamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CharingPower = table.Column<int>(type: "INTEGER", nullable: false),
                    PowerFromGrid = table.Column<int>(type: "INTEGER", nullable: false),
                    GridProportion = table.Column<float>(type: "REAL", nullable: false),
                    HandledChargeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerDistributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PowerDistributions_HandledCharges_HandledChargeId",
                        column: x => x.HandledChargeId,
                        principalTable: "HandledCharges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HandledCharges_ChargePriceId",
                table: "HandledCharges",
                column: "ChargePriceId");

            migrationBuilder.CreateIndex(
                name: "IX_PowerDistributions_HandledChargeId",
                table: "PowerDistributions",
                column: "HandledChargeId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PowerDistributions");

            migrationBuilder.DropTable(
                name: "HandledCharges");

            migrationBuilder.DropTable(
                name: "ChargePrices");
        }
    }
}
