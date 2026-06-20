using Domain.Events;
using Domain.Participants;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Tickets;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.CheckoutSessionId)
            .HasMaxLength(255);

        builder.Property(o => o.PaymentIntentId)
            .HasMaxLength(255);

        builder.HasOne(o => o.Event)
            .WithMany()
            .HasForeignKey(o => o.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.TicketType)
            .WithMany(t => t.Orders)
            .HasForeignKey(o => o.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Participant)
            .WithMany(p => p.Orders)
            .HasForeignKey(o => o.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.CheckoutSessionId)
            .IsUnique()
            .HasFilter("\"CheckoutSessionId\" IS NOT NULL");
    }
}
