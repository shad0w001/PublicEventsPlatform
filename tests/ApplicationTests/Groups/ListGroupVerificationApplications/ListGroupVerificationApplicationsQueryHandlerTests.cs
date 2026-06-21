using Application.Groups.GetGroupVerificationApplication;
using Application.Groups.ListGroupVerificationApplications;
using Application.Groups.SubmitGroupVerificationApplication;
using Domain.Groups;
using Domain.Users;

namespace ApplicationTests.Groups.ListGroupVerificationApplications;

public class ListGroupVerificationApplicationsQueryHandlerTests
{
    [Fact]
    public async Task ListGroupVerificationApplicationsQueryHandler_Should_ReturnApplications_When_Admin()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, owner) = await GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);
        var admin = GroupVerificationTestData.CreateUser(
            "auth0|list-admin",
            "list-admin@example.com",
            role: ServiceRole.Admin);

        await using (var seedContext = GroupVerificationTestData.CreateContext(databaseName))
        {
            seedContext.Users.Add(admin);
            await seedContext.SaveChangesAsync();

            var submitHandler = new SubmitGroupVerificationApplicationCommandHandler(
                seedContext,
                GroupVerificationTestData.CreateCurrentUserService(
                    seedContext,
                    GroupVerificationTestData.CreateVerifiedIdentity(owner)),
                GroupVerificationTestData.CreateVerifiedIdentity(owner),
                new Application.Groups.Services.GroupAccessService(seedContext),
                new Application.Groups.Services.GroupVerificationEligibilityService(seedContext));

            await submitHandler.Handle(
                new SubmitGroupVerificationApplicationCommand(group.Id),
                CancellationToken.None);
        }

        await using var context = GroupVerificationTestData.CreateContext(databaseName);
        var handler = new ListGroupVerificationApplicationsQueryHandler(
            context,
            GroupVerificationTestData.CreateVerifiedIdentity(admin, ServiceRole.Admin));

        // Act
        var result = await handler.Handle(
            new ListGroupVerificationApplicationsQuery(),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(group.Id, result.Value[0].GroupId);
        Assert.Equal(GroupVerificationApplicationStatus.Pending, result.Value[0].Status);
    }

    [Fact]
    public async Task GetGroupVerificationApplicationQueryHandler_Should_ReturnEligibilitySnapshot_When_Admin()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var (group, owner) = await GroupVerificationTestData.SeedEligibleGroupAsync(databaseName);
        var admin = GroupVerificationTestData.CreateUser(
            "auth0|detail-admin",
            "detail-admin@example.com",
            role: ServiceRole.Admin);

        Guid applicationId;
        await using (var seedContext = GroupVerificationTestData.CreateContext(databaseName))
        {
            seedContext.Users.Add(admin);
            await seedContext.SaveChangesAsync();

            var submitHandler = new SubmitGroupVerificationApplicationCommandHandler(
                seedContext,
                GroupVerificationTestData.CreateCurrentUserService(
                    seedContext,
                    GroupVerificationTestData.CreateVerifiedIdentity(owner)),
                GroupVerificationTestData.CreateVerifiedIdentity(owner),
                new Application.Groups.Services.GroupAccessService(seedContext),
                new Application.Groups.Services.GroupVerificationEligibilityService(seedContext));

            applicationId = (await submitHandler.Handle(
                new SubmitGroupVerificationApplicationCommand(group.Id),
                CancellationToken.None)).Value.Id;
        }

        await using var context = GroupVerificationTestData.CreateContext(databaseName);
        var handler = new GetGroupVerificationApplicationQueryHandler(
            context,
            GroupVerificationTestData.CreateVerifiedIdentity(admin, ServiceRole.Admin),
            new Application.Groups.Services.GroupVerificationEligibilityService(context));

        // Act
        var result = await handler.Handle(
            new GetGroupVerificationApplicationQuery(applicationId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Eligibility.MeetsEligibility);
        Assert.Equal(GroupVerificationConstants.MinVerifiedMembers, result.Value.Eligibility.MinVerifiedMembers);
        Assert.Equal(GroupVerificationConstants.MinCompletedEvents, result.Value.Eligibility.MinCompletedEvents);
    }
}
