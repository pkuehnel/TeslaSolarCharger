using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class MakeConfigurationKeysUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TscConfigurations_Key",
                table: "TscConfigurations",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TscConfigurations_Key",
                table: "TscConfigurations");
        }
    }
}
