using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHandledChargeChargePriceRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HandledCharges_ChargePrices_ChargePriceId",
                table: "HandledCharges");

            migrationBuilder.DropIndex(
                name: "IX_HandledCharges_ChargePriceId",
                table: "HandledCharges");

            migrationBuilder.DropColumn(
                name: "ChargePriceId",
                table: "HandledCharges");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChargePriceId",
                table: "HandledCharges",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_HandledCharges_ChargePriceId",
                table: "HandledCharges",
                column: "ChargePriceId");

            migrationBuilder.AddForeignKey(
                name: "FK_HandledCharges_ChargePrices_ChargePriceId",
                table: "HandledCharges",
                column: "ChargePriceId",
                principalTable: "ChargePrices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
