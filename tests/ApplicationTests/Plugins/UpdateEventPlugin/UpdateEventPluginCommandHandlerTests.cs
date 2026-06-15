using System.Text.Json;
using Application.Abstractions.Authentication;
using Application.Events.Services;
using Application.Plugins.AttachEventPlugin;
using Application.Plugins.UpdateEventPlugin;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.Services;
using Domain.Plugins;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Plugins.UpdateEventPlugin;

public class UpdateEventPluginCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public async Task UpdateEventPluginCommandHandler_Should_ReplaceAllData_When_UpdateSucceeds()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-plugin", "update@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var attachHandler = CreateAttachHandler(context, identity);
        await attachHandler.Handle(
            new AttachEventPluginCommand(eventId, pluginId, ValidFaqData("Old Q?", "Old answer.")),
            CancellationToken.None);

        var handler = CreateUpdateHandler(context, identity);
        var updatedData = ValidFaqData("New Q?", "New answer.");
        var command = new UpdateEventPluginCommand(eventId, pluginId, updatedData);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(pluginId, result.Value.PluginId);
        Assert.Contains(PluginConstants.DataKeyEntries, result.Value.Data.Keys);
        Assert.Contains("New Q?", result.Value.Data[PluginConstants.DataKeyEntries]);
    }

    [Fact]
    public async Task UpdateEventPluginCommandHandler_Should_ReturnNotAttached_When_PluginNotOnEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-not-attached", "na@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateUpdateHandler(context, identity);
        var command = new UpdateEventPluginCommand(eventId, pluginId, ValidFaqData());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.NotAttached", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventPluginCommandHandler_Should_ReturnInvalidData_When_ReplacementDataIsInvalid()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-invalid", "bad@example.com");
        var pluginId = await SeedCatalogAsync(databaseName);
        var eventId = await SeedBigDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var attachHandler = CreateAttachHandler(context, identity);
        await attachHandler.Handle(
            new AttachEventPluginCommand(eventId, pluginId, ValidFaqData()),
            CancellationToken.None);

        var handler = CreateUpdateHandler(context, identity);
        var command = new UpdateEventPluginCommand(
            eventId,
            pluginId,
            new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = "[]" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.InvalidData", result.Error.Code);
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

        var createResult = EventService.Create(EventTier.Big, "Big Draft", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static Dictionary<string, string?> ValidFaqData(
        string question = "What time?",
        string answer = "At 6 PM.")
    {
        var entries = JsonSerializer.Serialize(new[] { new { question, answer } });
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

    private static UpdateEventPluginCommandHandler CreateUpdateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new UpdateEventPluginCommandHandler(
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
