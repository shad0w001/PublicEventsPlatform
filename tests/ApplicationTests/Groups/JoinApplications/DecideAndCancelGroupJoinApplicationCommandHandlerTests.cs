using Application.Abstractions.Authentication;
using Application.Groups.CancelGroupJoinApplication;
using Application.Groups.DecideGroupJoinApplication;
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

namespace ApplicationTests.Groups.JoinApplications;

public class DecideAndCancelGroupJoinApplicationCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private static readonly DateTime UtcNow = new(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DecideGroupJoinApplicationCommandHandler_Should_ApproveApplication_When_ActorCanReview()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|decide-owner", "owner@example.com");
        var applicant = CreateUser("auth0|decide-applicant", "applicant@example.com");
        var (group, application) = SeedPendingApplication(databaseName, owner, applicant);
        var identity = CreateVerifiedIdentity("auth0|decide-owner", "owner@example.com");

        await using var context = CreateContext(databaseName);
        var handler = new DecideGroupJoinApplicationCommandHandler(
            context,
            CreateCurrentUserService(context, identity),
            identity,
            new GroupAccessService(context));

        // Act
        var result = await handler.Handle(
            new DecideGroupJoinApplicationCommand(group.Id, application.Id, JoinApplicationDecision.Approve),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var membership = await verifyContext.GroupMemberships
            .SingleOrDefaultAsync(m => m.UserId == applicant.Id);
        Assert.NotNull(membership);
    }

    [Fact]
    public async Task CancelGroupJoinApplicationCommandHandler_Should_RemovePendingApplication_When_ActorIsApplicant()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|cancel-owner", "owner@example.com");
        var applicant = CreateUser("auth0|cancel-applicant", "applicant@example.com");
        var (group, application) = SeedPendingApplication(databaseName, owner, applicant);
        var identity = CreateVerifiedIdentity("auth0|cancel-applicant", "applicant@example.com");

        await using var context = CreateContext(databaseName);
        var handler = new CancelGroupJoinApplicationCommandHandler(
            context,
            CreateCurrentUserService(context, identity),
            identity,
            new GroupAccessService(context));

        // Act
        var result = await handler.Handle(
            new CancelGroupJoinApplicationCommand(group.Id, application.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        Assert.Empty(await verifyContext.GroupJoinApplications.ToListAsync());
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static (Group Group, GroupJoinApplication Application) SeedPendingApplication(
        string databaseName,
        User owner,
        User applicant)
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

        return (group, application);
    }

    private static CurrentUserService CreateCurrentUserService(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

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
