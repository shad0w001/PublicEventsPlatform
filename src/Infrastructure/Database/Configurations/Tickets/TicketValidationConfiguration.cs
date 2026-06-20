using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Tickets;

public class TicketValidationConfiguration : IEntityTypeConfiguration<TicketValidation>
{
    public void Configure(EntityTypeBuilder<TicketValidation> builder)
    {
        builder.ToTable("ticket_validations");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Method)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(v => v.Ticket)
            .WithMany(t => t.Validations)
            .HasForeignKey(v => v.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.TicketId);
    }
}
