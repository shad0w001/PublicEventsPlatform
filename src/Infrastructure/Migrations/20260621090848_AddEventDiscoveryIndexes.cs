using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventDiscoveryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_events_Status_StartTime",
                schema: "public",
                table: "events",
                columns: new[] { "Status", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_event_locations_City",
                schema: "public",
                table: "event_locations",
                column: "City");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_events_Status_StartTime",
                schema: "public",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_event_locations_City",
                schema: "public",
                table: "event_locations");
        }
    }
}
