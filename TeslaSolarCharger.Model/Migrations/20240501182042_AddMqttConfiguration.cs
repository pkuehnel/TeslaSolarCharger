using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddMqttConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MqttConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Password = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MqttConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MqttResultConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NodePatternType = table.Column<int>(type: "INTEGER", nullable: false),
                    MqttConfigurationId = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrectionFactor = table.Column<decimal>(type: "TEXT", nullable: false),
                    UsedFor = table.Column<int>(type: "INTEGER", nullable: false),
                    Operator = table.Column<int>(type: "INTEGER", nullable: false),
                    NodePattern = table.Column<string>(type: "TEXT", nullable: true),
                    XmlAttributeHeaderName = table.Column<string>(type: "TEXT", nullable: true),
                    XmlAttributeHeaderValue = table.Column<string>(type: "TEXT", nullable: true),
                    XmlAttributeValueName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MqttResultConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MqttResultConfigurations_MqttConfigurations_MqttConfigurationId",
                        column: x => x.MqttConfigurationId,
                        principalTable: "MqttConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MqttResultConfigurations_MqttConfigurationId",
                table: "MqttResultConfigurations",
                column: "MqttConfigurationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MqttResultConfigurations");

            migrationBuilder.DropTable(
                name: "MqttConfigurations");
        }
    }
}
