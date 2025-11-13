using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameActorToParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actors",
                schema: "public");

            migrationBuilder.DropTable(
                name: "group_members",
                schema: "public");

            migrationBuilder.DropTable(
                name: "groups",
                schema: "public");

            migrationBuilder.DropPrimaryKey(
                name: "PK_event_attendees",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                schema: "public",
                table: "users");

            migrationBuilder.RenameTable(
                name: "users",
                schema: "public",
                newName: "participants",
                newSchema: "public");

            migrationBuilder.RenameColumn(
                name: "ActorId",
                schema: "public",
                table: "event_organizers",
                newName: "ParticipantId");

            migrationBuilder.RenameColumn(
                name: "AttendeeId",
                schema: "public",
                table: "event_attendees",
                newName: "EventId1");

            migrationBuilder.RenameIndex(
                name: "IX_users_Email",
                schema: "public",
                table: "participants",
                newName: "IX_participants_Email");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "public",
                table: "event_attendees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

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
                name: "ParticipantId",
                schema: "public",
                table: "event_attendees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "public",
                table: "participants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ProfilePictureUrl",
                schema: "public",
                table: "participants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "public",
                table: "participants",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastActive",
                schema: "public",
                table: "participants",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "public",
                table: "participants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "public",
                table: "participants",
                type: "character varying(1000)",
                maxLength: 1000,
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
                name: "ProfileImageUrl",
                schema: "public",
                table: "participants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_event_attendees",
                schema: "public",
                table: "event_attendees",
                columns: new[] { "EventId", "ParticipantId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_participants",
                schema: "public",
                table: "participants",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "group_memberships",
                schema: "public",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_memberships", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_group_memberships_participants_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "public",
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_memberships_participants_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_organizers_ParticipantId",
                schema: "public",
                table: "event_organizers",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_event_attendees_EventId1",
                schema: "public",
                table: "event_attendees",
                column: "EventId1");

            migrationBuilder.CreateIndex(
                name: "IX_event_attendees_ParticipantId",
                schema: "public",
                table: "event_attendees",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_group_memberships_UserId",
                schema: "public",
                table: "group_memberships",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_event_attendees_events_EventId1",
                schema: "public",
                table: "event_attendees",
                column: "EventId1",
                principalSchema: "public",
                principalTable: "events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_event_attendees_participants_ParticipantId",
                schema: "public",
                table: "event_attendees",
                column: "ParticipantId",
                principalSchema: "public",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_event_organizers_participants_ParticipantId",
                schema: "public",
                table: "event_organizers",
                column: "ParticipantId",
                principalSchema: "public",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_attendees_events_EventId1",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropForeignKey(
                name: "FK_event_attendees_participants_ParticipantId",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropForeignKey(
                name: "FK_event_organizers_participants_ParticipantId",
                schema: "public",
                table: "event_organizers");

            migrationBuilder.DropTable(
                name: "group_memberships",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_event_organizers_ParticipantId",
                schema: "public",
                table: "event_organizers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_event_attendees",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropIndex(
                name: "IX_event_attendees_EventId1",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropIndex(
                name: "IX_event_attendees_ParticipantId",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropPrimaryKey(
                name: "PK_participants",
                schema: "public",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "ParticipantId",
                schema: "public",
                table: "event_attendees");

            migrationBuilder.DropColumn(
                name: "Description",
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
                name: "ProfileImageUrl",
                schema: "public",
                table: "participants");

            migrationBuilder.RenameTable(
                name: "participants",
                schema: "public",
                newName: "users",
                newSchema: "public");

            migrationBuilder.RenameColumn(
                name: "ParticipantId",
                schema: "public",
                table: "event_organizers",
                newName: "ActorId");

            migrationBuilder.RenameColumn(
                name: "EventId1",
                schema: "public",
                table: "event_attendees",
                newName: "AttendeeId");

            migrationBuilder.RenameIndex(
                name: "IX_participants_Email",
                schema: "public",
                table: "users",
                newName: "IX_users_Email");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "public",
                table: "event_attendees",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "RegisteredAt",
                schema: "public",
                table: "event_attendees",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "public",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProfilePictureUrl",
                schema: "public",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "public",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastActive",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "public",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_event_attendees",
                schema: "public",
                table: "event_attendees",
                columns: new[] { "EventId", "AttendeeId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                schema: "public",
                table: "users",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "actors",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                schema: "public",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_members", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_group_members_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "public",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_members_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_members_UserId",
                schema: "public",
                table: "group_members",
                column: "UserId");
        }
    }
}
