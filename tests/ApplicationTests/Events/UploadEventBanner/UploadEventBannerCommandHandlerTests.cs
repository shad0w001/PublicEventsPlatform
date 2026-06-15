using Application.Abstractions.Authentication;
using Application.Abstractions.Media;
using Application.Events.UploadEventBanner;
using Application.Events.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace ApplicationTests.Events.UploadEventBanner;

public class UploadEventBannerCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string UploadedBannerUrl = "/uploads/events/test-banner.webp";

    [Fact]
    public async Task UploadEventBannerCommandHandler_Should_UpdateBannerUrl_When_EditorUploads()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|banner-upload", "banner@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity, new FakeMediaStorageService(UploadedBannerUrl));

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var upload = new MediaUploadRequest(content, "image/jpeg", content.Length, "banner.jpg");

        // Act
        var result = await handler.Handle(
            new UploadEventBannerCommand(eventId, upload),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UploadedBannerUrl, result.Value.BannerImageUrl);
    }

    [Fact]
    public async Task UploadEventBannerCommandHandler_Should_ReturnInsufficientPermissions_When_NonEditorUploads()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|banner-owner", "owner@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, ownerIdentity);

        var strangerIdentity = CreateVerifiedIdentity("auth0|banner-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity, new FakeMediaStorageService(UploadedBannerUrl));

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var upload = new MediaUploadRequest(content, "image/jpeg", content.Length, "banner.jpg");

        // Act
        var result = await handler.Handle(
            new UploadEventBannerCommand(eventId, upload),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UploadEventBannerCommandHandler_Should_ReturnNotFound_When_EventIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|banner-deleted", "deleted@example.com");
        var eventId = await SeedSoftDeletedDraftAsync(databaseName, identity);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity, new FakeMediaStorageService(UploadedBannerUrl));

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var upload = new MediaUploadRequest(content, "image/jpeg", content.Length, "banner.jpg");

        // Act
        var result = await handler.Handle(
            new UploadEventBannerCommand(eventId, upload),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Deleted", result.Error.Code);
    }

    private static async Task<Guid> SeedDraftEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Banner Event", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedSoftDeletedDraftAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Deleted Banner Event", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        EventService.SoftDelete(@event);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static UploadEventBannerCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity,
        IMediaStorageService mediaStorageService)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new UploadEventBannerCommandHandler(
            context,
            currentUserService,
            identity,
            new EventAccessService(context),
            mediaStorageService);
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
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

    private sealed class FakeMediaStorageService(string url) : IMediaStorageService
    {
        public Task<Result<string>> SaveAsync(
            MediaPurpose purpose,
            Guid entityId,
            MediaUploadRequest upload,
            CancellationToken cancellationToken) =>
            Task.FromResult<Result<string>>(url);

        public Task TryDeleteLocalFileAsync(string? url, CancellationToken cancellationToken) =>
            Task.CompletedTask;
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
