using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Tickets;
using Application.Users.Services;
using Domain.Events;
using Domain.Tickets;
using Domain.Users.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tickets.ListMyTickets;

internal sealed class ListMyTicketsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService) : IQueryHandler<ListMyTicketsQuery, MyTicketsResponse>
{
    public async Task<Result<MyTicketsResponse>> Handle(
        ListMyTicketsQuery query,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<MyTicketsResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<MyTicketsResponse>(userResult.Error);
        }

        var user = userResult.Value;
        var organizerPlusGroupIds = await eventAccessService.GetOrganizerPlusGroupIdsAsync(
            user.Id,
            cancellationToken);
        var groupIdSet = organizerPlusGroupIds.ToHashSet();
        var participantIds = organizerPlusGroupIds.Append(user.Id).ToList();

        var tickets = await context.Tickets
            .AsNoTracking()
            .Include(t => t.TicketType)
            .Include(t => t.TicketCode)
            .Include(t => t.Validations)
            .Include(t => t.Order)
            .Include(t => t.Event)
            .Where(t => participantIds.Contains(t.ParticipantId))
            .Where(t => t.Order.Status == OrderStatus.Paid)
            .Where(t => t.Event.DeletedAt == null && t.Event.Status != EventStatus.Draft)
            .ToListAsync(cancellationToken);

        if (tickets.Count == 0)
        {
            return new MyTicketsResponse([], []);
        }

        var groupNames = groupIdSet.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Groups
                .AsNoTracking()
                .Where(g => groupIdSet.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        var selfDisplayName = !string.IsNullOrWhiteSpace(user.Username)
            ? user.Username
            : UserService.CreateDefaultUsernameFromEmail(user.Email);

        var items = tickets
            .Select(ticket =>
            {
                var participantIsGroup = groupIdSet.Contains(ticket.ParticipantId);
                return new MyTicketListItemResponse(
                    ticket.Id,
                    ticket.OrderId,
                    ticket.EventId,
                    ticket.Event.Title,
                    ticket.Event.Status,
                    ticket.Event.StartTime,
                    ticket.Event.EndTime,
                    ticket.Event.TimeZoneId,
                    ticket.TicketType.Name,
                    ticket.ParticipantId,
                    participantIsGroup,
                    ResolveParticipantDisplayName(
                        ticket.ParticipantId,
                        user.Id,
                        selfDisplayName,
                        groupIdSet,
                        groupNames),
                    TicketDisplayHelper.IsCheckedIn(ticket),
                    TicketDisplayHelper.IsRefundPending(ticket.Order, ticket.Event),
                    TicketDisplayHelper.GetCheckedInAt(ticket));
            })
            .OrderBy(i => i.StartTime)
            .ThenByDescending(i => i.TicketId)
            .ToList();

        var personal = items.Where(i => i.ParticipantId == user.Id).ToList();
        var group = items.Where(i => i.ParticipantId != user.Id).ToList();

        return new MyTicketsResponse(personal, group);
    }

    private static string ResolveParticipantDisplayName(
        Guid participantId,
        Guid userId,
        string selfDisplayName,
        IReadOnlySet<Guid> groupIds,
        IReadOnlyDictionary<Guid, string> groupNames)
    {
        if (participantId == userId)
        {
            return selfDisplayName;
        }

        if (groupIds.Contains(participantId))
        {
            return groupNames.GetValueOrDefault(participantId, "Organization");
        }

        return "Participant";
    }
}
