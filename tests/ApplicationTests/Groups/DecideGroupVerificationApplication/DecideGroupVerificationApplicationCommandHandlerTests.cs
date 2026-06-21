using Application.Groups.DecideGroupVerificationApplication;
using Application.Groups.SubmitGroupVerificationApplication;
using Domain.Groups;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Groups.DecideGroupVerificationApplication;

public class DecideGroupVerificationApplicationCommandHandlerTests
{
    [Fact]
    public async Task DecideGroupVerificationApplicationCommandHandler_Should_ApproveAndVerifyGroup_When_AdminApproves()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, owner) = await GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);
        var admin = GroupVerificationTestData.CreateUser(
            "auth0|platform-admin",
            "admin@example.com",
            role: ServiceRole.Admin);

        await using (var seedContext = GroupVerificationTestData.CreateContext(databaseName))
        {
            seedContext.Users.Add(admin);
            await seedContext.SaveChangesAsync();
        }

        Guid applicationId;
        await using (var submitContext = GroupVerificationTestData.CreateContext(databaseName))
        {
            var submitHandler = new SubmitGroupVerificationApplicationCommandHandler(
                submitContext,
                GroupVerificationTestData.CreateCurrentUserService(
                    submitContext,
                    GroupVerificationTestData.CreateVerifiedIdentity(owner)),
                GroupVerificationTestData.CreateVerifiedIdentity(owner),
                new Application.Groups.Services.GroupAccessService(submitContext),
                new Application.Groups.Services.GroupVerificationEligibilityService(submitContext));

            var submitResult = await submitHandler.Handle(
                new SubmitGroupVerificationApplicationCommand(group.Id),
                CancellationToken.None);
            applicationId = submitResult.Value.Id;
        }

        await using var context = GroupVerificationTestData.CreateContext(databaseName);
        var decideHandler = CreateHandler(context, GroupVerificationTestData.CreateVerifiedIdentity(admin, ServiceRole.Admin));

        // Act
        var result = await decideHandler.Handle(
            new DecideGroupVerificationApplicationCommand(
                applicationId,
                VerificationApplicationDecision.Approve),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = GroupVerificationTestData.CreateContext(databaseName);
        var persistedGroup = await verifyContext.Groups.SingleAsync(g => g.Id == group.Id);
        Assert.True(persistedGroup.IsVerified);
        Assert.NotNull(persistedGroup.VerifiedAt);
    }

    [Fact]
    public async Task DecideGroupVerificationApplicationCommandHandler_Should_ReturnForbidden_When_NotAdmin()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, owner) = await GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);

        Guid applicationId;
        await using (var submitContext = GroupVerificationTestData.CreateContext(databaseName))
        {
            var submitHandler = new SubmitGroupVerificationApplicationCommandHandler(
                submitContext,
                GroupVerificationTestData.CreateCurrentUserService(
                    submitContext,
                    GroupVerificationTestData.CreateVerifiedIdentity(owner)),
                GroupVerificationTestData.CreateVerifiedIdentity(owner),
                new Application.Groups.Services.GroupAccessService(submitContext),
                new Application.Groups.Services.GroupVerificationEligibilityService(submitContext));

            applicationId = (await submitHandler.Handle(
                new SubmitGroupVerificationApplicationCommand(group.Id),
                CancellationToken.None)).Value.Id;
        }

        await using var context = GroupVerificationTestData.CreateContext(databaseName);
        var handler = CreateHandler(context, GroupVerificationTestData.CreateVerifiedIdentity(owner));

        // Act
        var result = await handler.Handle(
            new DecideGroupVerificationApplicationCommand(
                applicationId,
                VerificationApplicationDecision.Approve),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.InsufficientAdminPermissions", result.Error.Code);
    }

    private static DecideGroupVerificationApplicationCommandHandler CreateHandler(
        Infrastructure.Database.ApplicationDbContext context,
        GroupVerificationTestData.FakeUserIdentityAccessor identity) =>
        new(
            context,
            GroupVerificationTestData.CreateCurrentUserService(context, identity),
            identity,
            new Application.Groups.Services.GroupAccessService(context));
}
