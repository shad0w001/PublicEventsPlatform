using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceEventLocationDateWithSegmentTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Date",
                schema: "public",
                table: "event_locations");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndsAt",
                schema: "public",
                table: "event_locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartsAt",
                schema: "public",
                table: "event_locations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndsAt",
                schema: "public",
                table: "event_locations");

            migrationBuilder.DropColumn(
                name: "StartsAt",
                schema: "public",
                table: "event_locations");

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                schema: "public",
                table: "event_locations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
