using Domain.Events;
using Domain.Participants;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Tickets;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(t => t.Id);

        builder.HasOne(t => t.Order)
            .WithMany(o => o.Tickets)
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.TicketType)
            .WithMany(tt => tt.Tickets)
            .HasForeignKey(t => t.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Event)
            .WithMany()
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Participant)
            .WithMany(p => p.Tickets)
            .HasForeignKey(t => t.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.EventId);
        builder.HasIndex(t => t.ParticipantId);
    }
}
