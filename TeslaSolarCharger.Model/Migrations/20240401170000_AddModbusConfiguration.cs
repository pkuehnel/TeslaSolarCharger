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
                    RegisterType = table.Column<int>(type: "INTEGER", nullable: false),
                    ValueType = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<int>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Endianess = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectDelaySeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    BitStartIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    BitLength = table.Column<int>(type: "INTEGER", nullable: true),
                    InvertsModbusConfigurationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CorrectionFactor = table.Column<decimal>(type: "TEXT", nullable: false),
                    UsedFor = table.Column<int>(type: "INTEGER", nullable: false),
                    Operator = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModbusConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModbusConfigurations_ModbusConfigurations_InvertsModbusConfigurationId",
                        column: x => x.InvertsModbusConfigurationId,
                        principalTable: "ModbusConfigurations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModbusConfigurations_InvertsModbusConfigurationId",
                table: "ModbusConfigurations",
                column: "InvertsModbusConfigurationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModbusConfigurations");
        }
    }
}
