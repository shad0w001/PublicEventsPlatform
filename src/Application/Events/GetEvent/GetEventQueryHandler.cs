using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Users.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Events.GetEvent;

internal sealed class GetEventQueryHandler(
    IApplicationDbContext context,
    IUserIdentityAccessor identityAccessor,
    ICurrentUserService currentUserService,
    EventAccessService eventAccessService)
    : IQueryHandler<GetEventQuery, GetEventResponse>
{
    public async Task<Result<GetEventResponse>> Handle(
        GetEventQuery query,
        CancellationToken cancellationToken)
    {
        var eventResult = await eventAccessService.GetActiveEventAsync(query.EventId, cancellationToken);
        if (eventResult.IsFailure)
        {
            return Result.Failure<GetEventResponse>(eventResult.Error);
        }

        var @event = eventResult.Value;

        if (@event.Status == EventStatus.Draft)
        {
            return await HandleDraftGetAsync(@event, query.EventId, cancellationToken);
        }

        if (@event.Status is EventStatus.Published or EventStatus.Cancelled)
        {
            return await HandlePublicGetAsync(@event, cancellationToken);
        }

        return Result.Failure<GetEventResponse>(EventErrors.NotFound(query.EventId));
    }

    private async Task<Result<GetEventResponse>> HandleDraftGetAsync(
        Event @event,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        if (!identityAccessor.IsAuthenticated)
        {
            return Result.Failure<GetEventResponse>(EventErrors.NotFound(eventId));
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<GetEventResponse>(EventErrors.NotFound(eventId));
        }

        var user = userResult.Value;

        var editAccessResult = await eventAccessService.ResolveEditAccessAsync(
            @event,
            user.Id,
            user.Id,
            cancellationToken);

        if (editAccessResult.IsFailure)
        {
            return Result.Failure<GetEventResponse>(EventErrors.NotFound(eventId));
        }

        var categoryName = await EventCategoryLookup.ResolveNameAsync(
            context,
            @event.CategoryId,
            cancellationToken);

        return new GetEventResponse(
            Public: null,
            EditDetail: EventMapping.ToDetailResponse(@event, editAccessResult.Value, categoryName),
            CanEdit: true);
    }

    private async Task<Result<GetEventResponse>> HandlePublicGetAsync(
        Event @event,
        CancellationToken cancellationToken)
    {
        if (identityAccessor.IsAuthenticated)
        {
            var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
            if (userResult.IsSuccess)
            {
                var user = userResult.Value;
                var editAccessResult = await eventAccessService.ResolveEditAccessAsync(
                    @event,
                    user.Id,
                    user.Id,
                    cancellationToken);

                if (editAccessResult.IsSuccess)
                {
                    var editorCategoryName = await EventCategoryLookup.ResolveNameAsync(
                        context,
                        @event.CategoryId,
                        cancellationToken);

                    return new GetEventResponse(
                        Public: null,
                        EditDetail: EventMapping.ToDetailResponse(
                            @event,
                            editAccessResult.Value,
                            editorCategoryName),
                        CanEdit: true);
                }
            }
        }

        var hostParticipantId = eventAccessService.GetHostParticipantId(@event);

        var hostIsGroup = await context.Groups
            .AsNoTracking()
            .AnyAsync(g => g.Id == hostParticipantId, cancellationToken);

        var hostDisplayName = await ResolveHostDisplayNameAsync(
            hostParticipantId,
            hostIsGroup,
            cancellationToken);

        var categoryName = await EventCategoryLookup.ResolveNameAsync(
            context,
            @event.CategoryId,
            cancellationToken);

        var publicResponse = EventMapping.ToPublicResponse(
            @event,
            hostDisplayName,
            hostIsGroup,
            categoryName);

        return new GetEventResponse(publicResponse, EditDetail: null, CanEdit: false);
    }

    private async Task<string> ResolveHostDisplayNameAsync(
        Guid hostParticipantId,
        bool hostIsGroup,
        CancellationToken cancellationToken)
    {
        if (hostIsGroup)
        {
            var groupName = await context.Groups
                .AsNoTracking()
                .Where(g => g.Id == hostParticipantId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(cancellationToken);

            return groupName ?? "Organization";
        }

        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == hostParticipantId)
            .Select(u => new { u.Username, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return "Host";
        }

        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return user.Username;
        }

        return UserService.CreateDefaultUsernameFromEmail(user.Email);
    }
}
