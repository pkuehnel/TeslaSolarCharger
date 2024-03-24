using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddOldHandledChargeIdToNewChargingProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConvertedFromOldStructure",
                table: "ChargingProcesses");

            migrationBuilder.AddColumn<int>(
                name: "OldHandledChargeId",
                table: "ChargingProcesses",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OldHandledChargeId",
                table: "ChargingProcesses");

            migrationBuilder.AddColumn<bool>(
                name: "ConvertedFromOldStructure",
                table: "ChargingProcesses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
