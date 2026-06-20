using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Tickets;

public class TicketCodeConfiguration : IEntityTypeConfiguration<TicketCode>
{
    public void Configure(EntityTypeBuilder<TicketCode> builder)
    {
        builder.ToTable("ticket_codes");

        builder.HasKey(c => c.TicketId);

        builder.Property(c => c.ManualCode)
            .HasMaxLength(TicketConstants.ManualCodeLength)
            .IsRequired();

        builder.HasOne(c => c.Ticket)
            .WithOne(t => t.TicketCode)
            .HasForeignKey<TicketCode>(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.ManualCode)
            .IsUnique();
    }
}
