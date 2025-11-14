using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevertTPHBackToTPTForParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_group_memberships_participants_GroupId",
                schema: "public",
                table: "group_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_group_memberships_participants_UserId",
                schema: "public",
                table: "group_memberships");

            migrationBuilder.DropIndex(
                name: "IX_participants_Email",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "Bio",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "LastActive",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "ParticipantType",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "ProfileImageUrl",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "ProfilePictureUrl",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "Username",
                schema: "public",
                table: "participants");

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_groups_participants_Id",
                        column: x => x.Id,
                        principalSchema: "public",
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    ProfilePictureUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Bio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastActive = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_participants_Id",
                        column: x => x.Id,
                        principalSchema: "public",
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "public",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_group_memberships_groups_GroupId",
                schema: "public",
                table: "group_memberships",
                column: "GroupId",
                principalSchema: "public",
                principalTable: "groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_group_memberships_users_UserId",
                schema: "public",
                table: "group_memberships",
                column: "UserId",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_group_memberships_groups_GroupId",
                schema: "public",
                table: "group_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_group_memberships_users_UserId",
                schema: "public",
                table: "group_memberships");

            migrationBuilder.DropTable(
                name: "groups",
                schema: "public");

            migrationBuilder.DropTable(
                name: "users",
                schema: "public");

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                schema: "public",
                table: "participants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "public",
                table: "participants",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "public",
                table: "participants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActive",
                schema: "public",
                table: "participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "public",
                table: "participants",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParticipantType",
                schema: "public",
                table: "participants",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                schema: "public",
                table: "participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageUrl",
                schema: "public",
                table: "participants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureUrl",
                schema: "public",
                table: "participants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "public",
                table: "participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_Email",
                schema: "public",
                table: "participants",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_group_memberships_participants_GroupId",
                schema: "public",
                table: "group_memberships",
                column: "GroupId",
                principalSchema: "public",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_group_memberships_participants_UserId",
                schema: "public",
                table: "group_memberships",
                column: "UserId",
                principalSchema: "public",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
