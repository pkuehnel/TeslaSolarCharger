using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddLoggedErrors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoggedErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartTimeStamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTimeStamp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IssueKey = table.Column<string>(type: "TEXT", nullable: false),
                    Vin = table.Column<string>(type: "TEXT", nullable: true),
                    TelegramNotificationSent = table.Column<bool>(type: "INTEGER", nullable: false),
                    TelegramResolvedMessageSent = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoggedErrors", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoggedErrors");
        }
    }
}
