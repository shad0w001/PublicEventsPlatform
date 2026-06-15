using System.Text.Json;
using Domain.Events;
using Domain.Plugins;
using Domain.Plugins.Services;
using DomainTests.Events;

namespace DomainTests.Plugins;

public class PluginServiceAttachTests
{
    private static readonly DateTime EventStart = EventTestData.DefaultEventStart;
    private static readonly DateTime EventEnd = EventTestData.DefaultEventEnd;

    [Fact]
    public void PluginService_Should_AttachPlugin_When_BigTierEventHasValidData()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        EventTestData.MakePublishReady(eventEntity);
        var catalog = CreateFaqPlugin();
        var data = ValidFaqData();

        // Act
        var result = PluginService.Attach(eventEntity, catalog, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(eventEntity.Plugins);
        Assert.Equal(catalog.Id, eventEntity.Plugins[0].PluginId);
        Assert.True(eventEntity.Plugins[0].IsActive);
        Assert.Single(eventEntity.Plugins[0].Data);
        Assert.Equal(PluginConstants.DataKeyEntries, eventEntity.Plugins[0].Data[0].Key);
    }

    [Fact]
    public void PluginService_Should_ReturnBigTierRequired_When_EventIsSmallTier()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Small);
        var catalog = CreateFaqPlugin();

        // Act
        var result = PluginService.Attach(eventEntity, catalog, ValidFaqData());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.BigTierRequired", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ReturnMaxPluginsExceeded_When_EventAlreadyHasFivePlugins()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        var plugins = Enumerable.Range(0, PluginConstants.MaxPluginsPerEvent)
            .Select(_ => CreateFaqPlugin())
            .ToList();

        foreach (var plugin in plugins)
        {
            PluginService.Attach(eventEntity, plugin, ValidFaqData());
        }

        var extraPlugin = CreateFaqPlugin();

        // Act
        var result = PluginService.Attach(eventEntity, extraPlugin, ValidFaqData());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.MaxPluginsExceeded", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ReturnAlreadyAttached_When_PluginIsAlreadyOnEvent()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        var catalog = CreateFaqPlugin();
        PluginService.Attach(eventEntity, catalog, ValidFaqData());

        // Act
        var result = PluginService.Attach(eventEntity, catalog, ValidFaqData());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.AlreadyAttached", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ReplaceAllDataKeys_When_UpdateConfigIsCalled()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        EventTestData.MakePublishReady(eventEntity);
        var catalog = CreateFaqPlugin();
        var attachResult = PluginService.Attach(eventEntity, catalog, ValidFaqData());
        var usage = attachResult.Value;

        var updatedEntries = JsonSerializer.Serialize(new[]
        {
            new { question = "New Q?", answer = "New answer." },
            new { question = "Second?", answer = "Second answer." }
        });
        var newData = new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = updatedEntries };

        // Act
        var result = PluginService.UpdateConfig(
            usage,
            catalog.Code,
            newData,
            EventStart,
            EventEnd);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(usage.Data);
        Assert.Equal(updatedEntries, usage.Data[0].Value);
    }

    [Fact]
    public void PluginService_Should_RemoveUsage_When_DetachIsCalled()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        var catalog = CreateFaqPlugin();
        PluginService.Attach(eventEntity, catalog, ValidFaqData());

        // Act
        var result = PluginService.Detach(eventEntity, catalog.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(eventEntity.Plugins);
    }

    [Fact]
    public void PluginService_Should_ReturnNotAttached_When_DetachingUnknownPlugin()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        var missingPluginId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        // Act
        var result = PluginService.Detach(eventEntity, missingPluginId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.NotAttached", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ClearAllUsages_When_DetachAllIsCalled()
    {
        // Arrange
        var (eventEntity, _) = EventTestData.CreateDraft(EventTier.Big);
        PluginService.Attach(eventEntity, CreateFaqPlugin(), ValidFaqData());
        PluginService.Attach(eventEntity, CreateLinksPlugin(), ValidLinksData());

        // Act
        PluginService.DetachAll(eventEntity);

        // Assert
        Assert.Empty(eventEntity.Plugins);
    }

    private static Plugin CreateFaqPlugin() =>
        CreatePlugin(PluginConstants.CodeFaq, "FAQ");

    private static Plugin CreateLinksPlugin() =>
        CreatePlugin(PluginConstants.CodeLinks, "Important Links");

    private static Plugin CreatePlugin(string code, string name) =>
        new()
        {
            Code = code,
            Name = name,
            Description = "Test plugin",
            Version = "1.0.0"
        };

    private static Dictionary<string, string?> ValidFaqData()
    {
        var entries = JsonSerializer.Serialize(new[]
        {
            new { question = "What time?", answer = "At 6 PM." }
        });

        return new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = entries };
    }

    private static Dictionary<string, string?> ValidLinksData()
    {
        var links = JsonSerializer.Serialize(new[]
        {
            new { label = "Website", url = "https://example.com" }
        });

        return new Dictionary<string, string?> { [PluginConstants.DataKeyLinks] = links };
    }
}
