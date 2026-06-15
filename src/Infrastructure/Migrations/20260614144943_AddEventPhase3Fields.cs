using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPhase3Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdmissionType",
                schema: "public",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "public",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "public",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                schema: "public",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                schema: "public",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "Small");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                schema: "public",
                table: "events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_CreatedByUserId",
                schema: "public",
                table: "events",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_events_PublishedAt",
                schema: "public",
                table: "events",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_events_Status",
                schema: "public",
                table: "events",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_events_users_CreatedByUserId",
                schema: "public",
                table: "events",
                column: "CreatedByUserId",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            var seedCreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                schema: "public",
                table: "event_categories",
                columns: ["Id", "Name", "ParentCategoryId", "CreatedAt"],
                values: new object[,]
                {
                    { Guid.Parse("e0000001-0001-4000-8000-000000000001"), "Music", null, seedCreatedAt },
                    { Guid.Parse("e0000002-0001-4000-8000-000000000002"), "Sports", null, seedCreatedAt },
                    { Guid.Parse("e0000003-0001-4000-8000-000000000003"), "Technology", null, seedCreatedAt },
                    { Guid.Parse("e0000004-0001-4000-8000-000000000004"), "Arts & Culture", null, seedCreatedAt },
                    { Guid.Parse("e0000005-0001-4000-8000-000000000005"), "Food & Drink", null, seedCreatedAt },
                    { Guid.Parse("e0000006-0001-4000-8000-000000000006"), "Education", null, seedCreatedAt },
                    { Guid.Parse("e0000007-0001-4000-8000-000000000007"), "Community", null, seedCreatedAt },
                    { Guid.Parse("e0000008-0001-4000-8000-000000000008"), "Business", null, seedCreatedAt },
                    { Guid.Parse("e0000009-0001-4000-8000-000000000009"), "Health & Wellness", null, seedCreatedAt },
                    { Guid.Parse("e000000a-0001-4000-8000-00000000000a"), "Family & Kids", null, seedCreatedAt },
                    { Guid.Parse("e000000b-0001-4000-8000-00000000000b"), "Other", null, seedCreatedAt },
                    { Guid.Parse("e0000101-0001-4000-8000-000000000001"), "Live Music", Guid.Parse("e0000001-0001-4000-8000-000000000001"), seedCreatedAt },
                    { Guid.Parse("e0000102-0001-4000-8000-000000000002"), "DJ / Electronic", Guid.Parse("e0000001-0001-4000-8000-000000000001"), seedCreatedAt },
                    { Guid.Parse("e0000201-0001-4000-8000-000000000001"), "Conferences", Guid.Parse("e0000003-0001-4000-8000-000000000003"), seedCreatedAt }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "event_categories",
                keyColumn: "Id",
                keyValue: Guid.Parse("e0000201-0001-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "event_categories",
                keyColumn: "Id",
                keyValue: Guid.Parse("e0000102-0001-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "event_categories",
                keyColumn: "Id",
                keyValue: Guid.Parse("e0000101-0001-4000-8000-000000000001"));

            foreach (var categoryId in new[]
                     {
                         "e0000001-0001-4000-8000-000000000001",
                         "e0000002-0001-4000-8000-000000000002",
                         "e0000003-0001-4000-8000-000000000003",
                         "e0000004-0001-4000-8000-000000000004",
                         "e0000005-0001-4000-8000-000000000005",
                         "e0000006-0001-4000-8000-000000000006",
                         "e0000007-0001-4000-8000-000000000007",
                         "e0000008-0001-4000-8000-000000000008",
                         "e0000009-0001-4000-8000-000000000009",
                         "e000000a-0001-4000-8000-00000000000a",
                         "e000000b-0001-4000-8000-00000000000b"
                     })
            {
                migrationBuilder.DeleteData(
                    schema: "public",
                    table: "event_categories",
                    keyColumn: "Id",
                    keyValue: Guid.Parse(categoryId));
            }

            migrationBuilder.DropForeignKey(
                name: "FK_events_users_CreatedByUserId",
                schema: "public",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_CreatedByUserId",
                schema: "public",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_PublishedAt",
                schema: "public",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_Status",
                schema: "public",
                table: "events");

            migrationBuilder.DropColumn(
                name: "AdmissionType",
                schema: "public",
                table: "events");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "public",
                table: "events");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "public",
                table: "events");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                schema: "public",
                table: "events");

            migrationBuilder.DropColumn(
                name: "Tier",
                schema: "public",
                table: "events");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                schema: "public",
                table: "events");
        }
    }
}
