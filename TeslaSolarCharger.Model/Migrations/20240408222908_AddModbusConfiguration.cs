using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddModbusConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModbusConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnitIdentifier = table.Column<int>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Endianess = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectDelayMilliseconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadTimeoutMilliseconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModbusConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModbusResultConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegisterType = table.Column<int>(type: "INTEGER", nullable: false),
                    ValueType = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<int>(type: "INTEGER", nullable: false),
                    BitStartIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    BitLength = table.Column<int>(type: "INTEGER", nullable: true),
                    ModbusConfigurationId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvertsModbusResultConfigurationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CorrectionFactor = table.Column<decimal>(type: "TEXT", nullable: false),
                    UsedFor = table.Column<int>(type: "INTEGER", nullable: false),
                    Operator = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModbusResultConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModbusResultConfigurations_ModbusConfigurations_ModbusConfigurationId",
                        column: x => x.ModbusConfigurationId,
                        principalTable: "ModbusConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModbusResultConfigurations_ModbusResultConfigurations_InvertsModbusResultConfigurationId",
                        column: x => x.InvertsModbusResultConfigurationId,
                        principalTable: "ModbusResultConfigurations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModbusResultConfigurations_InvertsModbusResultConfigurationId",
                table: "ModbusResultConfigurations",
                column: "InvertsModbusResultConfigurationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModbusResultConfigurations_ModbusConfigurationId",
                table: "ModbusResultConfigurations",
                column: "ModbusConfigurationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModbusResultConfigurations");

            migrationBuilder.DropTable(
                name: "ModbusConfigurations");
        }
    }
}
