using Application.Search.Stubs;
using ApplicationTests.Notifications.Handlers;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Events;
using Domain.Events.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Search.Stubs;

public class EventPublishedEmbeddingStubHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CompleteWithoutThrow_When_ValidEventPublished()
    {
        // Arrange
        var handler = new EventPublishedEmbeddingStubHandler(
            NullLogger<EventPublishedEmbeddingStubHandler>.Instance);
        var eventId = Guid.NewGuid();
        var hostParticipantId = Guid.NewGuid();

        // Act
        var act = () => handler.HandleAsync(
            new EventPublished(eventId, hostParticipantId),
            CancellationToken.None);

        // Assert
        await act();
    }
}
