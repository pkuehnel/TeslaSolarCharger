using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterValueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeterValues_CarId",
                table: "MeterValues");

            migrationBuilder.CreateIndex(
                name: "IX_MeterValues_CarId_MeterValueKind_Timestamp",
                table: "MeterValues",
                columns: new[] { "CarId", "MeterValueKind", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeterValues_CarId_MeterValueKind_Timestamp",
                table: "MeterValues");

            migrationBuilder.CreateIndex(
                name: "IX_MeterValues_CarId",
                table: "MeterValues",
                column: "CarId");
        }
    }
}
