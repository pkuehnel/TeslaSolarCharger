using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class SaveRawCoolDownSecondsToDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PhaseSwitchCoolDownTime",
                table: "OcppChargingStationConnectors",
                newName: "PhaseSwitchCoolDownTimeSeconds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PhaseSwitchCoolDownTimeSeconds",
                table: "OcppChargingStationConnectors",
                newName: "PhaseSwitchCoolDownTime");
        }
    }
}
