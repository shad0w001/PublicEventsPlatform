using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Messaging;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Topic)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.OccurredOnUtc)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.Error)
            .HasMaxLength(2000);

        builder.HasIndex(m => m.PublishedAt)
            .HasFilter("\"PublishedAt\" IS NULL");
    }
}
