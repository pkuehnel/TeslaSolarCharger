using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddRestValueConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestValueConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    NodePatternType = table.Column<int>(type: "INTEGER", nullable: false),
                    HttpMethod = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestValueConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestValueConfigurationHeaders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    RestValueConfigurationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestValueConfigurationHeaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestValueConfigurationHeaders_RestValueConfigurations_RestValueConfigurationId",
                        column: x => x.RestValueConfigurationId,
                        principalTable: "RestValueConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RestValueResultConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NodePattern = table.Column<string>(type: "TEXT", nullable: true),
                    XmlAttributeHeaderName = table.Column<string>(type: "TEXT", nullable: true),
                    XmlAttributeHeaderValue = table.Column<string>(type: "TEXT", nullable: true),
                    XmlAttributeValueName = table.Column<string>(type: "TEXT", nullable: true),
                    CorrectionFactor = table.Column<decimal>(type: "TEXT", nullable: false),
                    UsedFor = table.Column<int>(type: "INTEGER", nullable: false),
                    Operator = table.Column<int>(type: "INTEGER", nullable: false),
                    RestValueConfigurationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestValueResultConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestValueResultConfigurations_RestValueConfigurations_RestValueConfigurationId",
                        column: x => x.RestValueConfigurationId,
                        principalTable: "RestValueConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestValueConfigurationHeaders_RestValueConfigurationId_Key",
                table: "RestValueConfigurationHeaders",
                columns: new[] { "RestValueConfigurationId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestValueResultConfigurations_RestValueConfigurationId",
                table: "RestValueResultConfigurations",
                column: "RestValueConfigurationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestValueConfigurationHeaders");

            migrationBuilder.DropTable(
                name: "RestValueResultConfigurations");

            migrationBuilder.DropTable(
                name: "RestValueConfigurations");
        }
    }
}
