using System.Text.Json;
using Application.Abstractions.Authentication;
using Application.Events.Services;
using Application.Plugins.AttachEventPlugin;
using Application.Plugins.DetachEventPlugin;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.Services;
using Domain.Plugins;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Plugins.DetachEventPlugin;

using ApplicationTests.Events;

public class DetachEventPluginCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public async Task DetachEventPluginCommandHandler_Should_RemoveAttachment_When_DetachSucceeds()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|detach-success", "detach@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var attachHandler = CreateAttachHandler(context, identity);
        await attachHandler.Handle(
            new AttachEventPluginCommand(eventId, pluginId, ValidFaqData()),
            CancellationToken.None);

        var handler = CreateDetachHandler(context, identity);
        var command = new DetachEventPluginCommand(eventId, pluginId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var persisted = await verifyContext.Events
            .Include(e => e.Plugins)
            .SingleAsync(e => e.Id == eventId);
        Assert.Empty(persisted.Plugins);
    }

    [Fact]
    public async Task DetachEventPluginCommandHandler_Should_ReturnNotAttached_When_PluginNotOnEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|detach-missing", "missing@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateDetachHandler(context, identity);
        var command = new DetachEventPluginCommand(eventId, pluginId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.NotAttached", result.Error.Code);
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

    private static Dictionary<string, string?> ValidFaqData()
    {
        var entries = JsonSerializer.Serialize(new[]
        {
            new { question = "What time?", answer = "At 6 PM." }
        });

        return new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = entries };
    }

    private static AttachEventPluginCommandHandler CreateAttachHandler(
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

    private static DetachEventPluginCommandHandler CreateDetachHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new DetachEventPluginCommandHandler(
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
