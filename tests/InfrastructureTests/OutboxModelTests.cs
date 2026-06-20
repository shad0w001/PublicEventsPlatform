using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class OutboxModelTests
{
    [Fact]
    public void OutboxMessageModel_Should_HaveUnpublishedIndex_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(OutboxMessage));
        var index = entityType?.GetIndexes()
            .SingleOrDefault(i => i.Properties.Any(p => p.Name == nameof(OutboxMessage.PublishedAt)));

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(index);
        Assert.NotNull(index.GetFilter());
    }

    [Fact]
    public void ProcessedMessageModel_Should_UseCompositeKey_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(ProcessedMessage));
        var keyProperties = entityType?.FindPrimaryKey()?.Properties.Select(p => p.Name).ToList();

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(keyProperties);
        Assert.Equal(
            new[] { nameof(ProcessedMessage.ConsumerName), nameof(ProcessedMessage.MessageId) },
            keyProperties);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
