using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                schema: "public",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_categories",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_categories_event_categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalSchema: "public",
                        principalTable: "event_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_CategoryId",
                schema: "public",
                table: "events",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_event_categories_ParentCategoryId",
                schema: "public",
                table: "event_categories",
                column: "ParentCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_categories_CategoryId",
                schema: "public",
                table: "events",
                column: "CategoryId",
                principalSchema: "public",
                principalTable: "event_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_event_categories_CategoryId",
                schema: "public",
                table: "events");

            migrationBuilder.DropTable(
                name: "event_categories",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_events_CategoryId",
                schema: "public",
                table: "events");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "public",
                table: "events");
        }
    }
}
