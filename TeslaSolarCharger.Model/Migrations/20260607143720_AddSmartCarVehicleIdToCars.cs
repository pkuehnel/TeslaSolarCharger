using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartCarVehicleIdToCars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SmartCarVehicleId",
                table: "Cars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cars_SmartCarVehicleId",
                table: "Cars",
                column: "SmartCarVehicleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cars_SmartCarVehicleId",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "SmartCarVehicleId",
                table: "Cars");
        }
    }
}
