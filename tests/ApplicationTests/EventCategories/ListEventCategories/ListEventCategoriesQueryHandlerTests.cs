using Application.EventCategories.ListEventCategories;
using Domain.Events;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.EventCategories.ListEventCategories;

public class ListEventCategoriesQueryHandlerTests
{
    [Fact]
    public async Task ListEventCategoriesQueryHandler_Should_ReturnNestedTree_When_CategoriesHaveParents()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await SeedCategoriesAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = new ListEventCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(new ListEventCategoriesQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var music = result.Value.Single(c => c.Name == "Music");
        Assert.Equal(2, music.SubCategories.Count);
        Assert.Contains(music.SubCategories, c => c.Name == "Live Music");
        Assert.Contains(music.SubCategories, c => c.Name == "DJ / Electronic");
        Assert.All(music.SubCategories, c => Assert.Empty(c.SubCategories));

        var technology = result.Value.Single(c => c.Name == "Technology");
        Assert.Single(technology.SubCategories);
        Assert.Equal("Conferences", technology.SubCategories[0].Name);
    }

    [Fact]
    public async Task ListEventCategoriesQueryHandler_Should_ReturnEmptySubCategories_When_RootHasNoChildren()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await SeedCategoriesAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = new ListEventCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(new ListEventCategoriesQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var sports = result.Value.Single(c => c.Name == "Sports");
        Assert.Empty(sports.SubCategories);
    }

    [Fact]
    public async Task ListEventCategoriesQueryHandler_Should_SortByName_When_BuildingTree()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await SeedCategoriesAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = new ListEventCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(new ListEventCategoriesQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(["Music", "Sports", "Technology"], result.Value.Select(c => c.Name).ToList());

        var music = result.Value.Single(c => c.Name == "Music");
        Assert.Equal(["DJ / Electronic", "Live Music"], music.SubCategories.Select(c => c.Name).ToList());
    }

    [Fact]
    public async Task ListEventCategoriesQueryHandler_Should_ReturnEmptyList_When_NoCategoriesExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var handler = new ListEventCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(new ListEventCategoriesQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    private static async Task SeedCategoriesAsync(string databaseName)
    {
        await using var context = CreateContext(databaseName);
        var music = new EventCategory { Name = "Music" };
        var sports = new EventCategory { Name = "Sports" };
        var technology = new EventCategory { Name = "Technology" };
        context.EventCategories.AddRange(music, sports, technology);
        await context.SaveChangesAsync(CancellationToken.None);

        context.EventCategories.AddRange(
            new EventCategory { Name = "Live Music", ParentCategoryId = music.Id },
            new EventCategory { Name = "DJ / Electronic", ParentCategoryId = music.Id },
            new EventCategory { Name = "Conferences", ParentCategoryId = technology.Id });
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
