using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixEntityRelationshipMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_attendees_events_EventId1",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropIndex(
                name: "IX_plugin_usages_EventId",
                schema: "public",
                table: "plugin_usages");

            migrationBuilder.DropIndex(
                name: "IX_event_attendees_EventId1",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropColumn(
                name: "EventId1",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RegisteredAt",
                schema: "public",
                table: "event_attendees",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_plugin_usages_EventId_PluginId",
                schema: "public",
                table: "plugin_usages",
                columns: new[] { "EventId", "PluginId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_plugin_usages_EventId_PluginId",
                schema: "public",
                table: "plugin_usages");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RegisteredAt",
                schema: "public",
                table: "event_attendees",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventId1",
                schema: "public",
                table: "event_attendees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_plugin_usages_EventId",
                schema: "public",
                table: "plugin_usages",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_event_attendees_EventId1",
                schema: "public",
                table: "event_attendees",
                column: "EventId1");

            migrationBuilder.AddForeignKey(
                name: "FK_event_attendees_events_EventId1",
                schema: "public",
                table: "event_attendees",
                column: "EventId1",
                principalSchema: "public",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
