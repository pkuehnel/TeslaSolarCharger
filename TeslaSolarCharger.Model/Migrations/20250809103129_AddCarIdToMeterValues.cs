using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddCarIdToMeterValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CarId",
                table: "MeterValues",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeterValues_CarId",
                table: "MeterValues",
                column: "CarId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MeterValue_CarId_Conditional",
                table: "MeterValues",
                sql: "(MeterValueKind = 6 AND CarId IS NOT NULL) OR (MeterValueKind != 6 AND CarId IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_MeterValues_Cars_CarId",
                table: "MeterValues",
                column: "CarId",
                principalTable: "Cars",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeterValues_Cars_CarId",
                table: "MeterValues");

            migrationBuilder.DropIndex(
                name: "IX_MeterValues_CarId",
                table: "MeterValues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MeterValue_CarId_Conditional",
                table: "MeterValues");

            migrationBuilder.DropColumn(
                name: "CarId",
                table: "MeterValues");
        }
    }
}
