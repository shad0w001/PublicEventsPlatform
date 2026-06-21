using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_event_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "public",
                        principalTable: "event_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_CategoryId",
                schema: "public",
                table: "user_subscriptions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId",
                schema: "public",
                table: "user_subscriptions",
                column: "UserId",
                unique: true,
                filter: "\"Kind\" = 'Online'");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId_CategoryId",
                schema: "public",
                table: "user_subscriptions",
                columns: new[] { "UserId", "CategoryId" },
                unique: true,
                filter: "\"Kind\" = 'Category'");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId_City",
                schema: "public",
                table: "user_subscriptions",
                columns: new[] { "UserId", "City" },
                unique: true,
                filter: "\"Kind\" = 'City'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_subscriptions",
                schema: "public");
        }
    }
}
