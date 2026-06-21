using Application.Groups.Services;
using Application.Groups.SubmitGroupVerificationApplication;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Groups.SubmitGroupVerificationApplication;

public class SubmitGroupVerificationApplicationCommandHandlerTests
{
    [Fact]
    public async Task SubmitGroupVerificationApplicationCommandHandler_Should_CreatePendingApplication_When_EligibleOrganizerPlus()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, owner) = await GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);
        await using var context = GroupVerificationTestData.CreateContext(databaseName);
        var identity = GroupVerificationTestData.CreateVerifiedIdentity(owner);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SubmitGroupVerificationApplicationCommand(group.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(GroupVerificationApplicationStatus.Pending, result.Value.Status);
        Assert.Equal(group.Id, result.Value.GroupId);

        await using var verifyContext = GroupVerificationTestData.CreateContext(databaseName);
        Assert.Single(await verifyContext.GroupVerificationApplications.ToListAsync());
    }

    [Fact]
    public async Task SubmitGroupVerificationApplicationCommandHandler_Should_ReturnInsufficientPermissions_When_MemberOnly()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, owner) = await GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);
        var member = GroupVerificationTestData.CreateUser("auth0|plain-member", "plain@example.com");

        await using (var seedContext = GroupVerificationTestData.CreateContext(databaseName))
        {
            seedContext.Users.Add(member);
            seedContext.GroupMemberships.Add(
                GroupMembership.Create(group.Id, member.Id, GroupMemberRole.Member));
            await seedContext.SaveChangesAsync();
        }

        await using var context = GroupVerificationTestData.CreateContext(databaseName);
        var handler = CreateHandler(context, GroupVerificationTestData.CreateVerifiedIdentity(member));

        // Act
        var result = await handler.Handle(
            new SubmitGroupVerificationApplicationCommand(group.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task SubmitGroupVerificationApplicationCommandHandler_Should_ReturnInsufficientCompletedEvents_When_NotEligible()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = GroupVerificationTestData.CreateUser("auth0|ineligible-owner", "ineligible@example.com");
        var createResult = GroupService.Create(
            "Ineligible Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            GroupVerificationTestData.DefaultGroupImageUrl);
        var group = createResult.Value.Group;

        await using (var seedContext = GroupVerificationTestData.CreateContext(databaseName))
        {
            seedContext.Users.Add(owner);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
            await seedContext.SaveChangesAsync();
        }

        await using var context = GroupVerificationTestData.CreateContext(databaseName);
        var handler = CreateHandler(context, GroupVerificationTestData.CreateVerifiedIdentity(owner));

        // Act
        var result = await handler.Handle(
            new SubmitGroupVerificationApplicationCommand(group.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Groups.InsufficientVerifiedMembers", result.Error.Code);
    }

    [Fact]
    public async Task SubmitGroupVerificationApplicationCommandHandler_Should_ReturnEmailNotVerified_When_CallerUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, owner) = await GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);

        await using var context = GroupVerificationTestData.CreateContext(databaseName);
        var identity = GroupVerificationTestData.CreateVerifiedIdentity(owner);
        identity.EmailVerified = false;
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SubmitGroupVerificationApplicationCommand(group.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    private static SubmitGroupVerificationApplicationCommandHandler CreateHandler(
        Infrastructure.Database.ApplicationDbContext context,
        GroupVerificationTestData.FakeUserIdentityAccessor identity) =>
        new(
            context,
            GroupVerificationTestData.CreateCurrentUserService(context, identity),
            identity,
            new Application.Groups.Services.GroupAccessService(context),
            new GroupVerificationEligibilityService(context));
}
