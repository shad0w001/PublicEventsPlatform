using Application.Plugins.ListPlugins;
using Domain.Plugins;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Plugins.ListPlugins;

public class ListPluginsQueryHandlerTests
{
    [Fact]
    public async Task ListPluginsQueryHandler_Should_ReturnCatalogSortedByName_When_PluginsSeeded()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await SeedCatalogAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = new ListPluginsQueryHandler(context);

        // Act
        var result = await handler.Handle(new ListPluginsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal("Agenda", result.Value[0].Name);
        Assert.Equal(PluginConstants.CodeAgenda, result.Value[0].Code);
        Assert.Equal("FAQ", result.Value[1].Name);
        Assert.Equal(PluginConstants.CodeFaq, result.Value[1].Code);
        Assert.Equal("Important Links", result.Value[2].Name);
        Assert.Equal(PluginConstants.CodeLinks, result.Value[2].Code);
        Assert.All(result.Value, p => Assert.NotEqual(Guid.Empty, p.Id));
    }

    [Fact]
    public async Task ListPluginsQueryHandler_Should_ReturnEmptyList_When_NoPluginsInDatabase()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var handler = new ListPluginsQueryHandler(context);

        // Act
        var result = await handler.Handle(new ListPluginsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    private static async Task SeedCatalogAsync(string databaseName)
    {
        await using var context = CreateContext(databaseName);
        context.Plugins.AddRange(
            CreatePlugin(
                PluginConstants.CodeAgenda,
                "Agenda",
                "Event schedule with sessions, times, and optional speakers."),
            CreatePlugin(
                PluginConstants.CodeFaq,
                "FAQ",
                "Frequently asked questions and answers for attendees."),
            CreatePlugin(
                PluginConstants.CodeLinks,
                "Important Links",
                "Curated links to resources, materials, and related pages."));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static Plugin CreatePlugin(string code, string name, string description) =>
        new()
        {
            Code = code,
            Name = name,
            Description = description,
            Version = "1.0.0"
        };

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
