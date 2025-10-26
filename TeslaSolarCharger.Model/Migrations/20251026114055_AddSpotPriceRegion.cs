using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddSpotPriceRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnergyProvider",
                table: "ChargePrices");

            migrationBuilder.AddColumn<int>(
                name: "SpotPriceRegion",
                table: "ChargePrices",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpotPriceRegion",
                table: "ChargePrices");

            migrationBuilder.AddColumn<int>(
                name: "EnergyProvider",
                table: "ChargePrices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);
        }
    }
}
