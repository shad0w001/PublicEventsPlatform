using Infrastructure.Payments;

namespace InfrastructureTests.Payments;

public class StripeWebhookMetadataTests
{
    [Fact]
    public void TryParseMetadata_Should_ReturnTrue_When_AllKeysPresent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            ["orderId"] = orderId.ToString(),
            ["eventId"] = eventId.ToString(),
            ["ticketTypeId"] = ticketTypeId.ToString(),
        };

        // Act
        var ok = StripeWebhookVerifier.TryParseMetadata(
            metadata,
            out var parsedOrderId,
            out var parsedEventId,
            out var parsedTicketTypeId);

        // Assert
        Assert.True(ok);
        Assert.Equal(orderId, parsedOrderId);
        Assert.Equal(eventId, parsedEventId);
        Assert.Equal(ticketTypeId, parsedTicketTypeId);
    }

    [Fact]
    public void TryParseMetadata_Should_ReturnFalse_When_MetadataMissing()
    {
        // Act
        var ok = StripeWebhookVerifier.TryParseMetadata(
            null,
            out _,
            out _,
            out _);

        // Assert
        Assert.False(ok);
    }
}
