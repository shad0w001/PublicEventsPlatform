using Domain.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Infrastructure.Database.Configurations.Search;

public class EventEmbeddingConfiguration : IEntityTypeConfiguration<EventEmbedding>
{
    public void Configure(EntityTypeBuilder<EventEmbedding> builder)
    {
        builder.ToTable("event_embeddings");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventId)
            .IsRequired();

        builder.Property(e => e.Embedding)
            .IsRequired()
            .HasConversion(
                embedding => new Vector(embedding),
                vector => vector.ToArray())
            .HasColumnType($"vector({SearchConstants.EmbeddingDimensions})");

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        builder.HasOne(e => e.Event)
            .WithMany()
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
