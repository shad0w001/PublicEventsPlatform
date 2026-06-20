using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Messaging;

public sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");

        builder.HasKey(m => new { m.ConsumerName, m.MessageId });

        builder.Property(m => m.ConsumerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.ProcessedAt)
            .IsRequired();
    }
}
