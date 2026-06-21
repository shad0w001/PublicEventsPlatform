using Domain.Search;
using Domain.Search.Services;
using SharedKernel;

namespace DomainTests.Search;

public class EventEmbeddingServiceTests
{
    [Fact]
    public void ValidateDimensions_Should_ReturnFailure_When_LengthIsNot1536()
    {
        // Arrange
        var embedding = new float[768];

        // Act
        var result = EventEmbeddingService.ValidateDimensions(embedding);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_Should_ReturnEmbedding_When_DimensionsAre1536()
    {
        // Arrange
        var embedding = new float[SearchConstants.EmbeddingDimensions];
        var eventId = Guid.NewGuid();
        var updatedAt = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = EventEmbeddingService.Create(eventId, embedding, updatedAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(eventId, result.Value.EventId);
        Assert.Equal(updatedAt, result.Value.UpdatedAt);
        Assert.Equal(SearchConstants.EmbeddingDimensions, result.Value.Embedding.Length);
    }
}
