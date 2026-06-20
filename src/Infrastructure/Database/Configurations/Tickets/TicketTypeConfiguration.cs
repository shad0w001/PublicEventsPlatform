using Domain.Events;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Tickets;

public class TicketTypeConfiguration : IEntityTypeConfiguration<TicketType>
{
    public void Configure(EntityTypeBuilder<TicketType> builder)
    {
        builder.ToTable("ticket_types");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(TicketConstants.NameMaxLength)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(TicketConstants.DescriptionMaxLength)
            .IsRequired();

        builder.Property(t => t.PriceCents)
            .IsRequired();

        builder.Property(t => t.Capacity)
            .IsRequired();

        builder.Property(t => t.SoldQuantity)
            .IsRequired();

        builder.Property(t => t.ReservedQuantity)
            .IsRequired();

        builder.HasOne(t => t.Event)
            .WithMany(e => e.TicketTypes)
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.EventId);
    }
}
