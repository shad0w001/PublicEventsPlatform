using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_types",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PriceCents = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    SoldQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_types_events_EventId",
                        column: x => x.EventId,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CheckoutSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PaymentIntentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_orders_events_EventId",
                        column: x => x.EventId,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "public",
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalSchema: "public",
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tickets_events_EventId",
                        column: x => x.EventId,
                        principalSchema: "public",
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "public",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tickets_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "public",
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_ticket_types_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalSchema: "public",
                        principalTable: "ticket_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_codes",
                schema: "public",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManualCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_codes", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_ticket_codes_tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_validations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_validations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_validations_tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_CheckoutSessionId",
                schema: "public",
                table: "orders",
                column: "CheckoutSessionId",
                unique: true,
                filter: "\"CheckoutSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_EventId",
                schema: "public",
                table: "orders",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ParticipantId",
                schema: "public",
                table: "orders",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_TicketTypeId",
                schema: "public",
                table: "orders",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_codes_ManualCode",
                schema: "public",
                table: "ticket_codes",
                column: "ManualCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_types_EventId",
                schema: "public",
                table: "ticket_types",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_validations_TicketId",
                schema: "public",
                table: "ticket_validations",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_EventId",
                schema: "public",
                table: "tickets",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_OrderId",
                schema: "public",
                table: "tickets",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_ParticipantId",
                schema: "public",
                table: "tickets",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_TicketTypeId",
                schema: "public",
                table: "tickets",
                column: "TicketTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_codes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_validations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_types",
                schema: "public");
        }
    }
}
