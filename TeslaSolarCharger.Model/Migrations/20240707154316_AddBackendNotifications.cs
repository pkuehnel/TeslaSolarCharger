using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeslaSolarCharger.Model.Migrations
{
    /// <inheritdoc />
    public partial class AddBackendNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackendNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BackendIssueId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Headline = table.Column<string>(type: "TEXT", nullable: false),
                    DetailText = table.Column<string>(type: "TEXT", nullable: false),
                    ValidFromDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValidToDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValidFromVersion = table.Column<string>(type: "TEXT", nullable: true),
                    ValidToVersion = table.Column<string>(type: "TEXT", nullable: true),
                    IsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackendNotifications", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackendNotifications");
        }
    }
}
