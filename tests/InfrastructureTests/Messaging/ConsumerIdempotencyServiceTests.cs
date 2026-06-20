using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests.Messaging;

public class ConsumerIdempotencyServiceTests
{
    [Fact]
    public async Task HasBeenProcessedAsync_Should_ReturnFalse_When_MessageNotProcessed()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var service = CreateService(databaseName);
        var messageId = Guid.CreateVersion7();

        // Act
        var processed = await service.HasBeenProcessedAsync(
            NotificationConsumerNames.TicketPurchaseCompleted,
            messageId,
            CancellationToken.None);

        // Assert
        Assert.False(processed);
    }

    [Fact]
    public async Task MarkProcessedAsync_Should_AllowHasBeenProcessedToReturnTrue_When_SameMessageMarkedTwice()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var service = CreateService(databaseName);
        var messageId = Guid.CreateVersion7();

        // Act
        await service.MarkProcessedAsync(
            NotificationConsumerNames.TicketPurchaseCompleted,
            messageId,
            CancellationToken.None);
        var processed = await service.HasBeenProcessedAsync(
            NotificationConsumerNames.TicketPurchaseCompleted,
            messageId,
            CancellationToken.None);

        // Assert
        Assert.True(processed);
    }

    [Fact]
    public async Task MarkProcessedAsync_Should_NotThrow_When_DuplicateInsertOccurs()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var messageId = Guid.CreateVersion7();

        await using (var context = CreateContext(databaseName))
        {
            context.ProcessedMessages.Add(new ProcessedMessage
            {
                ConsumerName = NotificationConsumerNames.EventRsvpStatusChanged,
                MessageId = messageId,
                ProcessedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService(databaseName);

        // Act
        var act = () => service.MarkProcessedAsync(
            NotificationConsumerNames.EventRsvpStatusChanged,
            messageId,
            CancellationToken.None);

        // Assert
        await act();
    }

    private static ConsumerIdempotencyService CreateService(string databaseName) =>
        new(CreateContext(databaseName));

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
