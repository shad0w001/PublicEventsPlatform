using Application.Search;
using Domain.Events.Events;

namespace ApplicationTests.Search;

public class EventCancelledEmbeddingHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CallDeleteAsync_When_EventCancelled()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var recordingIndexService = new RecordingEmbeddingIndexService();
        var handler = new EventCancelledEmbeddingHandler(recordingIndexService);

        // Act
        await handler.HandleAsync(new EventCancelled(eventId), CancellationToken.None);

        // Assert
        Assert.Equal([eventId], recordingIndexService.DeletedEventIds);
    }
}
