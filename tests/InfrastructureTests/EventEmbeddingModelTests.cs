using Domain.Search;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfrastructureTests;

public class EventEmbeddingModelTests
{
    [Fact]
    public void EventEmbeddingModel_Should_MapToEventEmbeddingsTable_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(EventEmbedding));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal("event_embeddings", entityType.GetTableName());
        Assert.NotNull(entityType.FindProperty(nameof(EventEmbedding.Embedding)));
        Assert.NotNull(entityType.FindProperty(nameof(EventEmbedding.UpdatedAt)));
    }

    [Fact]
    public void EventEmbeddingModel_Should_UseEventIdAsPrimaryKey_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(EventEmbedding));
        var primaryKey = entityType?.FindPrimaryKey();

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal(nameof(EventEmbedding.EventId), primaryKey.Properties[0].Name);
    }

    [Fact]
    public void EventEmbeddingModel_Should_NotContainShadowEventId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(EventEmbedding));
        var shadowProperty = entityType?.FindProperty("EventId1");

        // Assert
        Assert.NotNull(entityType);
        Assert.Null(shadowProperty);
    }

    [Fact]
    public void EventEmbeddingModel_Should_MapEmbeddingColumn_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(EventEmbedding));
        var embeddingProperty = entityType?.FindProperty(nameof(EventEmbedding.Embedding));

        // Assert
        Assert.NotNull(embeddingProperty);
        Assert.Equal(
            $"vector({SearchConstants.EmbeddingDimensions})",
            embeddingProperty.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5433;Database=public_events_platform;Username=postgres;Password=postgres",
                npgsqlOptions => npgsqlOptions.UseVector())
            .Options;

        return new ApplicationDbContext(options);
    }
}
