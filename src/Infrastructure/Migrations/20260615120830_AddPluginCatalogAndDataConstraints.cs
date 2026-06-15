using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginCatalogAndDataConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_plugin_data_PluginUsageId",
                schema: "public",
                table: "plugin_data");

            migrationBuilder.DropColumn(
                name: "Author",
                schema: "public",
                table: "plugins");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "public",
                table: "plugins",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                schema: "public",
                table: "plugin_data",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_plugins_Code",
                schema: "public",
                table: "plugins",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plugin_data_PluginUsageId_Key",
                schema: "public",
                table: "plugin_data",
                columns: new[] { "PluginUsageId", "Key" },
                unique: true);

            var seedCreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                schema: "public",
                table: "plugins",
                columns: ["Id", "Code", "Name", "Description", "Version", "CreatedAt"],
                values: new object[,]
                {
                    {
                        Guid.Parse("a0000001-0001-4000-8000-000000000001"),
                        "agenda",
                        "Agenda",
                        "Event schedule with sessions, times, and optional speakers.",
                        "1.0.0",
                        seedCreatedAt
                    },
                    {
                        Guid.Parse("a0000002-0001-4000-8000-000000000002"),
                        "faq",
                        "FAQ",
                        "Frequently asked questions and answers for attendees.",
                        "1.0.0",
                        seedCreatedAt
                    },
                    {
                        Guid.Parse("a0000003-0001-4000-8000-000000000003"),
                        "links",
                        "Important Links",
                        "Curated links to resources, materials, and related pages.",
                        "1.0.0",
                        seedCreatedAt
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "plugins",
                keyColumn: "Id",
                keyValue: Guid.Parse("a0000003-0001-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "plugins",
                keyColumn: "Id",
                keyValue: Guid.Parse("a0000002-0001-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "plugins",
                keyColumn: "Id",
                keyValue: Guid.Parse("a0000001-0001-4000-8000-000000000001"));

            migrationBuilder.DropIndex(
                name: "IX_plugins_Code",
                schema: "public",
                table: "plugins");

            migrationBuilder.DropIndex(
                name: "IX_plugin_data_PluginUsageId_Key",
                schema: "public",
                table: "plugin_data");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "public",
                table: "plugins");

            migrationBuilder.AddColumn<string>(
                name: "Author",
                schema: "public",
                table: "plugins",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                schema: "public",
                table: "plugin_data",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_plugin_data_PluginUsageId",
                schema: "public",
                table: "plugin_data",
                column: "PluginUsageId");
        }
    }
}
