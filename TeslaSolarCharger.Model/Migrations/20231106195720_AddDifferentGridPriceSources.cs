using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddDifferentGridPriceSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EnergyProvider",
                table: "ChargePrices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<string>(
                name: "EnergyProviderConfiguration",
                table: "ChargePrices",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnergyProvider",
                table: "ChargePrices");

            migrationBuilder.DropColumn(
                name: "EnergyProviderConfiguration",
                table: "ChargePrices");
        }
    }
}
