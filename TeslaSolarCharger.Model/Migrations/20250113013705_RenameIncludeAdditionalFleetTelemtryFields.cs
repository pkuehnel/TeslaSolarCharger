using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class RenameIncludeAdditionalFleetTelemtryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UseFleetTelemetryForLocationData",
                table: "Cars",
                newName: "IncludeTrackingRelevantFields");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IncludeTrackingRelevantFields",
                table: "Cars",
                newName: "UseFleetTelemetryForLocationData");
        }
    }
}
