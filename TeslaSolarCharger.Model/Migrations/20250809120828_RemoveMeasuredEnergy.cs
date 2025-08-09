using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMeasuredEnergy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedPower",
                table: "MeterValues");

            migrationBuilder.DropColumn(
                name: "MeasuredEnergyWs",
                table: "MeterValues");

            migrationBuilder.AlterColumn<int>(
                name: "MeasuredPower",
                table: "MeterValues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MeasuredPower",
                table: "MeterValues",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedPower",
                table: "MeterValues",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MeasuredEnergyWs",
                table: "MeterValues",
                type: "INTEGER",
                nullable: true);
        }
    }
}
