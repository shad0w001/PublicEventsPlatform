using Domain.Events.Services;

namespace DomainTests.Events;

public class EventPublishRateLimitTests
{
    [Fact]
    public void EventService_Should_AllowPublish_When_UnderRateLimit()
    {
        // Arrange
        const int maxPerWeek = 6;

        // Act
        var result = EventService.ValidatePublishRateLimit(recentPublishCount: 5, maxPerWeek);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EventService_Should_ReturnPublishRateLimitExceeded_When_AtLimit()
    {
        // Arrange
        const int maxPerWeek = 6;

        // Act
        var result = EventService.ValidatePublishRateLimit(recentPublishCount: 6, maxPerWeek);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.PublishRateLimitExceeded", result.Error.Code);
    }
}
