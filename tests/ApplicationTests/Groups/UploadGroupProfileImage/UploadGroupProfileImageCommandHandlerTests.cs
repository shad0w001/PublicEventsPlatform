using Application.Abstractions.Authentication;
using Application.Abstractions.Media;
using Application.Groups.UploadGroupProfileImage;
using Application.Groups.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace ApplicationTests.Groups.UploadGroupProfileImage;

public class UploadGroupProfileImageCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private const string UploadedImageUrl = "/uploads/groups/test-profile.webp";

    [Fact]
    public async Task UploadGroupProfileImageCommandHandler_Should_UpdateProfileImageUrl_When_AdministratorUploads()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|img-owner", "owner@example.com");
        var admin = CreateUser("auth0|img-admin", "admin@example.com");
        var group = SeedGroupWithMember(databaseName, owner, admin, GroupMemberRole.Administrator);

        var adminIdentity = CreateVerifiedIdentity("auth0|img-admin", "admin@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, adminIdentity, new FakeMediaStorageService(UploadedImageUrl));

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var upload = new MediaUploadRequest(content, "image/jpeg", content.Length, "profile.jpg");

        // Act
        var result = await handler.Handle(
            new UploadGroupProfileImageCommand(group.Id, upload),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UploadedImageUrl, result.Value.ProfileImageUrl);
    }

    [Fact]
    public async Task UploadGroupProfileImageCommandHandler_Should_ReturnInsufficientPermissions_When_MemberUploads()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|img-member-owner", "mowner@example.com");
        var member = CreateUser("auth0|img-member", "member@example.com");
        var group = SeedGroupWithMember(databaseName, owner, member, GroupMemberRole.Member);

        var memberIdentity = CreateVerifiedIdentity("auth0|img-member", "member@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, memberIdentity, new FakeMediaStorageService(UploadedImageUrl));

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var upload = new MediaUploadRequest(content, "image/jpeg", content.Length, "profile.jpg");

        // Act
        var result = await handler.Handle(
            new UploadGroupProfileImageCommand(group.Id, upload),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InsufficientPermissions", result.Error.Code);
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static Group SeedGroupWithMember(
        string databaseName,
        User owner,
        User member,
        GroupMemberRole memberRole)
    {
        var createResult = GroupService.Create(
            "Image Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var membership = GroupMembership.Create(group.Id, member.Id, memberRole);

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.AddRange(owner, member);
        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
        if (member.Id != owner.Id)
        {
            seedContext.GroupMemberships.Add(membership);
        }

        seedContext.SaveChanges();
        return group;
    }

    private static UploadGroupProfileImageCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity,
        IMediaStorageService mediaStorageService)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new UploadGroupProfileImageCommandHandler(
            context,
            currentUserService,
            identity,
            new GroupAccessService(context),
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
