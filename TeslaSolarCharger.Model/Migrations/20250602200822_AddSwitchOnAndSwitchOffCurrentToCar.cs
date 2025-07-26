using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddSwitchOnAndSwitchOffCurrentToCar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SwitchOffAtCurrent",
                table: "Cars",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SwitchOnAtCurrent",
                table: "Cars",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SwitchOffAtCurrent",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "SwitchOnAtCurrent",
                table: "Cars");
        }
    }
}
