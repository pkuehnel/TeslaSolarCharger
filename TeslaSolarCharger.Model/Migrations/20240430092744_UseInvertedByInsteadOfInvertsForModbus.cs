using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class UseInvertedByInsteadOfInvertsForModbus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModbusResultConfigurations_ModbusResultConfigurations_InvertsModbusResultConfigurationId",
                table: "ModbusResultConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ModbusResultConfigurations_InvertsModbusResultConfigurationId",
                table: "ModbusResultConfigurations");

            migrationBuilder.RenameColumn(
                name: "InvertsModbusResultConfigurationId",
                table: "ModbusResultConfigurations",
                newName: "InvertedByModbusResultConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModbusResultConfigurations_InvertedByModbusResultConfigurationId",
                table: "ModbusResultConfigurations",
                column: "InvertedByModbusResultConfigurationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModbusResultConfigurations_ModbusResultConfigurations_InvertedByModbusResultConfigurationId",
                table: "ModbusResultConfigurations",
                column: "InvertedByModbusResultConfigurationId",
                principalTable: "ModbusResultConfigurations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModbusResultConfigurations_ModbusResultConfigurations_InvertedByModbusResultConfigurationId",
                table: "ModbusResultConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ModbusResultConfigurations_InvertedByModbusResultConfigurationId",
                table: "ModbusResultConfigurations");

            migrationBuilder.RenameColumn(
                name: "InvertedByModbusResultConfigurationId",
                table: "ModbusResultConfigurations",
                newName: "InvertsModbusResultConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModbusResultConfigurations_InvertsModbusResultConfigurationId",
                table: "ModbusResultConfigurations",
                column: "InvertsModbusResultConfigurationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ModbusResultConfigurations_ModbusResultConfigurations_InvertsModbusResultConfigurationId",
                table: "ModbusResultConfigurations",
                column: "InvertsModbusResultConfigurationId",
                principalTable: "ModbusResultConfigurations",
                principalColumn: "Id");
        }
    }
}
