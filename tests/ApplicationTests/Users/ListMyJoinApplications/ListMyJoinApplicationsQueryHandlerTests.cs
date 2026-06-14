using Application.Abstractions.Authentication;
using Application.Users.ListMyJoinApplications;
using Application.Users.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Users.ListMyJoinApplications;

public class ListMyJoinApplicationsQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private static readonly DateTime UtcNow = new(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListMyJoinApplicationsQueryHandler_Should_ReturnAllUserApplicationsAcrossGroups_When_ApplicationsExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|global-owner", "owner@example.com");
        var applicant = CreateUser("auth0|global-applicant", "applicant@example.com");

        var groupOneResult = GroupService.Create("Org One", "", GroupJoinPolicy.ApplicationRequired, owner.Id, DefaultGroupImageUrl);
        var groupTwoResult = GroupService.Create("Org Two", "", GroupJoinPolicy.ApplicationRequired, owner.Id, DefaultGroupImageUrl);
        var appOne = GroupService.SubmitJoinApplication(groupOneResult.Value.Group, applicant.Id, UtcNow).Value;
        var appTwo = GroupService.SubmitJoinApplication(groupTwoResult.Value.Group, applicant.Id, UtcNow).Value;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(owner, applicant);
            seedContext.Groups.AddRange(groupOneResult.Value.Group, groupTwoResult.Value.Group);
            seedContext.GroupMemberships.AddRange(
                groupOneResult.Value.OwnerMembership,
                groupTwoResult.Value.OwnerMembership);
            seedContext.GroupJoinApplications.AddRange(appOne, appTwo);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|global-applicant", "applicant@example.com");
        await using var context = CreateContext(databaseName);
        var handler = new ListMyJoinApplicationsQueryHandler(
            context,
            new CurrentUserService(
                context,
                identity,
                Options.Create(new Application.Users.UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity);

        // Act
        var result = await handler.Handle(new ListMyJoinApplicationsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, a => a.GroupName == "Org One");
        Assert.Contains(result.Value, a => a.GroupName == "Org Two");
    }

    [Fact]
    public async Task ListMyJoinApplicationsQueryHandler_Should_ReturnEmptyList_When_UserHasNoApplications()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|no-apps", "noapps@example.com");

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();
        }

        var identity = CreateVerifiedIdentity("auth0|no-apps", "noapps@example.com");
        await using var context = CreateContext(databaseName);
        var handler = new ListMyJoinApplicationsQueryHandler(
            context,
            new CurrentUserService(
                context,
                identity,
                Options.Create(new Application.Users.UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity);

        // Act
        var result = await handler.Handle(new ListMyJoinApplicationsQuery(), CancellationToken.None);

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
