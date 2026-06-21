using Domain.Groups;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class GroupVerificationModelTests
{
    [Fact]
    public void GroupVerificationApplicationModel_Should_MapToGroupVerificationApplicationsTable_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(GroupVerificationApplication));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal("group_verification_applications", entityType.GetTableName());
        Assert.NotNull(entityType.FindProperty(nameof(GroupVerificationApplication.Status)));
        Assert.NotNull(entityType.FindProperty(nameof(GroupVerificationApplication.SubmittedByUserId)));
    }

    [Fact]
    public void GroupVerificationApplicationModel_Should_NotContainShadowGroupId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(GroupVerificationApplication));
        var shadowProperty = entityType?.FindProperty("GroupId1");

        // Assert
        Assert.NotNull(entityType);
        Assert.Null(shadowProperty);
    }

    [Fact]
    public void GroupVerificationApplicationModel_Should_HaveFilteredUniquePendingIndex_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(GroupVerificationApplication));
        var indexes = entityType?.GetIndexes().ToList() ?? [];

        var pendingIndex = indexes.SingleOrDefault(i =>
            i.Properties.Count == 1 &&
            i.Properties.Any(p => p.Name == nameof(GroupVerificationApplication.GroupId)) &&
            i.IsUnique &&
            i.GetFilter()?.Contains("Pending") == true);

        // Assert
        Assert.NotNull(pendingIndex);
    }

    [Fact]
    public void GroupModel_Should_ExposeVerificationColumns_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(Group));

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(entityType.FindProperty(nameof(Group.IsVerified)));
        Assert.NotNull(entityType.FindProperty(nameof(Group.VerifiedAt)));
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
