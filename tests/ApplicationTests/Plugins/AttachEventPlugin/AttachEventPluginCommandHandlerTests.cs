using System.Text.Json;
using Application.Abstractions.Authentication;
using Application.Events.Services;
using Application.Plugins.AttachEventPlugin;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Plugins;
using Domain.Plugins.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Plugins.AttachEventPlugin;

using ApplicationTests.Events;

public class AttachEventPluginCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public async Task AttachEventPluginCommandHandler_Should_ReturnEventPluginResponse_When_AttachSucceeds()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|attach-success", "attach@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var command = new AttachEventPluginCommand(eventId, pluginId, ValidFaqData());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(pluginId, result.Value.PluginId);
        Assert.Equal(PluginConstants.CodeFaq, result.Value.Code);
        Assert.Equal("FAQ", result.Value.Name);
        Assert.Contains(PluginConstants.DataKeyEntries, result.Value.Data.Keys);
        Assert.True(result.Value.AttachedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task AttachEventPluginCommandHandler_Should_ReturnNotFound_When_PluginDoesNotExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|attach-missing-plugin", "missing@example.com");
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var missingPluginId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var command = new AttachEventPluginCommand(eventId, missingPluginId, ValidFaqData());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AttachEventPluginCommandHandler_Should_ReturnAlreadyAttached_When_PluginIsDuplicate()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|attach-duplicate", "dup@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var command = new AttachEventPluginCommand(eventId, pluginId, ValidFaqData());
        await handler.Handle(command, CancellationToken.None);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.AlreadyAttached", result.Error.Code);
    }

    [Fact]
    public async Task AttachEventPluginCommandHandler_Should_ReturnBigTierRequired_When_EventIsSmallTier()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|attach-small", "small@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedSmallDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var command = new AttachEventPluginCommand(eventId, pluginId, ValidFaqData());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.BigTierRequired", result.Error.Code);
    }

    [Fact]
    public async Task AttachEventPluginCommandHandler_Should_ReturnInvalidData_When_DataIsInvalid()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|attach-invalid", "invalid@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var command = new AttachEventPluginCommand(
            eventId,
            pluginId,
            new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = "not-json" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.InvalidData", result.Error.Code);
    }

    [Fact]
    public async Task AttachEventPluginCommandHandler_Should_ReturnMaxPluginsExceeded_When_EventHasFivePlugins()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|attach-max", "max@example.com");
        var pluginIds = await SeedExtendedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        foreach (var pluginId in pluginIds.Take(PluginConstants.MaxPluginsPerEvent))
        {
            await handler.Handle(
                new AttachEventPluginCommand(eventId, pluginId, ValidFaqData()),
                CancellationToken.None);
        }

        var command = new AttachEventPluginCommand(
            eventId,
            pluginIds[PluginConstants.MaxPluginsPerEvent],
            ValidFaqData());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.MaxPluginsExceeded", result.Error.Code);
    }

    [Fact]
    public async Task AttachEventPluginCommandHandler_Should_ReturnInsufficientPermissions_When_NonEditorAttaches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|attach-owner", "owner@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, ownerIdentity);
        var strangerIdentity = CreateVerifiedIdentity("auth0|attach-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);
        var command = new AttachEventPluginCommand(eventId, pluginId, ValidFaqData());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task AttachEventPluginCommandHandler_Should_ReturnCannotModifyCancelled_When_EventIsCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|attach-cancelled", "cancelled@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedCancelledBigEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var command = new AttachEventPluginCommand(eventId, pluginId, ValidFaqData());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CannotModifyCancelled", result.Error.Code);
    }

    private static async Task<Guid> SeedCatalogAsync(string databaseName)
    {
        await using var context = CreateContext(databaseName);
        var plugin = new Plugin
        {
            Code = PluginConstants.CodeFaq,
            Name = "FAQ",
            Description = "FAQ plugin",
            Version = "1.0.0"
        };
        context.Plugins.Add(plugin);
        await context.SaveChangesAsync(CancellationToken.None);
        return plugin.Id;
    }

    private static async Task<List<Guid>> SeedExtendedCatalogAsync(string databaseName)
    {
        await using var context = CreateContext(databaseName);
        var ids = new List<Guid>();
        for (var i = 0; i < PluginConstants.MaxPluginsPerEvent + 1; i++)
        {
            var plugin = new Plugin
            {
                Code = PluginConstants.CodeFaq,
                Name = $"FAQ {i}",
                Description = "Test",
                Version = "1.0.0"
            };
            context.Plugins.Add(plugin);
            ids.Add(plugin.Id);
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return ids;
    }

    private static async Task<Guid> SeedBigDraftEventAsync(string databaseName, FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Big, "Big Draft", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedSmallDraftEventAsync(string databaseName, FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Small Draft", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedCancelledBigEventAsync(string databaseName, FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Big, "Cancelled Big", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var category = new EventCategory { Name = "Music" };
        context.EventCategories.Add(category);

        var start = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        var patch = new EventUpdatePatch
        {
            Description = "A test event description",
            CategoryId = category.Id,
            StartTime = start,
            EndTime = end,
            TimeZoneId = "Europe/Sofia",
            AdmissionType = AdmissionType.Free,
            Locations =
            [
                new EventLocation
                {
                    Name = "Main Hall",
                    Kind = EventLocationKind.Physical,
                    Address = "123 Main St",
                    City = "Sofia"
                }
            ]
        };

        EventService.Update(@event, patch, user.Id);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
        EventService.Cancel(@event);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static Dictionary<string, string?> ValidFaqData()
    {
        var entries = JsonSerializer.Serialize(new[]
        {
            new { question = "What time?", answer = "At 6 PM." }
        });

        return new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = entries };
    }

    private static AttachEventPluginCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new AttachEventPluginCommandHandler(
            context,
            currentUserService,
            identity,
            new EventAccessService(context));
    }

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string externalSubjectId, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = externalSubjectId,
            Email = email,
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeUserIdentityAccessor : IUserIdentityAccessor
    {
        public bool IsAuthenticated { get; init; }
        public string ExternalSubjectId { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool EmailVerified { get; init; }
        public ServiceRole ServiceRole { get; init; }
        public string? ProfilePictureUrl { get; init; }
    }
}
