using Application.Abstractions.Authentication;
using Application.Groups.ListGroupJoinApplications;
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

namespace ApplicationTests.Groups.ListGroupJoinApplications;

public class ListGroupJoinApplicationsQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private static readonly DateTime UtcNow = new(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListGroupJoinApplicationsQueryHandler_Should_ReturnAllApplications_When_ActorCanReview()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|list-owner", "owner@example.com");
        var applicant = CreateUser("auth0|list-applicant", "applicant@example.com");
        var group = SeedGroupWithApplication(databaseName, owner, applicant);
        var identity = CreateVerifiedIdentity("auth0|list-owner", "owner@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListGroupJoinApplicationsQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(applicant.Id, result.Value[0].UserId);
    }

    [Fact]
    public async Task ListGroupJoinApplicationsQueryHandler_Should_ReturnOwnApplicationsOnly_When_ActorIsNotModerator()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|list-owner2", "owner2@example.com");
        var applicant = CreateUser("auth0|list-applicant2", "applicant2@example.com");
        var other = CreateUser("auth0|list-other", "other@example.com");
        var group = SeedGroupWithApplication(databaseName, owner, applicant);

        await using (var seedContext = CreateContext(databaseName))
        {
            var otherApplication = GroupService.SubmitJoinApplication(group, other.Id, UtcNow).Value;
            seedContext.Users.Add(other);
            seedContext.GroupJoinApplications.Add(otherApplication);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|list-applicant2", "applicant2@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListGroupJoinApplicationsQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(applicant.Id, result.Value[0].UserId);
    }

    [Fact]
    public async Task ListGroupJoinApplicationsQueryHandler_Should_ReturnEmptyList_When_NoApplicationsForGroup()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|empty-owner", "empty-owner@example.com");
        var group = SeedOpenGroup(databaseName, owner);
        var identity = CreateVerifiedIdentity("auth0|empty-owner", "empty-owner@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListGroupJoinApplicationsQuery(group.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static Group SeedGroupWithApplication(string databaseName, User owner, User applicant)
    {
        var createResult = GroupService.Create(
            "App Org",
            "",
            GroupJoinPolicy.ApplicationRequired,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var application = GroupService.SubmitJoinApplication(group, applicant.Id, UtcNow).Value;

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.AddRange(owner, applicant);
        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
        seedContext.GroupJoinApplications.Add(application);
        seedContext.SaveChanges();

        return group;
    }

    private static Group SeedOpenGroup(string databaseName, User owner)
    {
        var createResult = GroupService.Create(
            "Open Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.Add(owner);
        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
        seedContext.SaveChanges();

        return group;
    }

    private static ListGroupJoinApplicationsQueryHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            new CurrentUserService(
                context,
                identity,
                Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new GroupAccessService(context));

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string subject, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = subject,
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
