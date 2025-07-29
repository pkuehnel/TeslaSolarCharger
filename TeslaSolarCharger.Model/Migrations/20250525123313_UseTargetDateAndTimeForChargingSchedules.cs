using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class UseTargetDateAndTimeForChargingSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NextOccurrence",
                table: "CarChargingSchedules",
                newName: "TargetTime");

            migrationBuilder.AddColumn<long>(
                name: "TargetDate",
                table: "CarChargingSchedules",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetDate",
                table: "CarChargingSchedules");

            migrationBuilder.RenameColumn(
                name: "TargetTime",
                table: "CarChargingSchedules",
                newName: "NextOccurrence");
        }
    }
}
