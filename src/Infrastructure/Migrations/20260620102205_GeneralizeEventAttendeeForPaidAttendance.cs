using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeEventAttendeeForPaidAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckedInAt",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "public",
                table: "event_attendees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "TicketCount",
                schema: "public",
                table: "event_attendees",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TicketCount",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "public",
                table: "event_attendees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedInAt",
                schema: "public",
                table: "event_attendees",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
