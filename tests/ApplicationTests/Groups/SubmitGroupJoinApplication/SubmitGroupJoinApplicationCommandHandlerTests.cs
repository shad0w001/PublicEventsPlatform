using Application.Abstractions.Authentication;
using Application.Groups.Services;
using Application.Groups.SubmitGroupJoinApplication;
using Application.Users;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Groups.SubmitGroupJoinApplication;

public class SubmitGroupJoinApplicationCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task SubmitGroupJoinApplicationCommandHandler_Should_CreatePendingApplication_When_PolicyRequiresApplication()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|app-owner", "owner@example.com");
        var applicant = CreateUser("auth0|applicant", "applicant@example.com");
        var group = SeedApplicationRequiredGroup(databaseName, owner);
        var identity = CreateVerifiedIdentity("auth0|applicant", "applicant@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SubmitGroupJoinApplicationCommand(group.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupJoinApplicationStatus.Pending, result.Value.Status);

        await using var verifyContext = CreateContext(databaseName);
        Assert.Single(await verifyContext.GroupJoinApplications.ToListAsync());
    }

    [Fact]
    public async Task SubmitGroupJoinApplicationCommandHandler_Should_ReturnApplicationsNotRequired_When_GroupIsOpen()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|open-owner", "open@example.com");
        var applicant = CreateUser("auth0|open-applicant", "applicant@example.com");

        var createResult = GroupService.Create(
            "Open Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(owner, applicant);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, CreateVerifiedIdentity("auth0|open-applicant", "applicant@example.com"));

        // Act
        var result = await handler.Handle(
            new SubmitGroupJoinApplicationCommand(group.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.ApplicationsNotRequired", result.Error.Code);
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static Group SeedApplicationRequiredGroup(string databaseName, User owner)
    {
        var createResult = GroupService.Create(
            "App Org",
            "",
            GroupJoinPolicy.ApplicationRequired,
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

    private static SubmitGroupJoinApplicationCommandHandler CreateHandler(
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
