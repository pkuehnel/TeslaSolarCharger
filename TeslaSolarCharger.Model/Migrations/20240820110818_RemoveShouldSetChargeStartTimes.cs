using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class RemoveShouldSetChargeStartTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShouldSetChargeStartTimes",
                table: "Cars");

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreLatestTimeToReachSocDateOnWeekend",
                table: "Cars",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IgnoreLatestTimeToReachSocDateOnWeekend",
                table: "Cars");

            migrationBuilder.AddColumn<bool>(
                name: "ShouldSetChargeStartTimes",
                table: "Cars",
                type: "INTEGER",
                nullable: true);
        }
    }
}
