using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Events.Services;
using Application.Users.Services;
using Domain.Events;
using Domain.Tickets;
using Domain.Tickets.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tickets.ValidateEventTicket;

internal sealed class ValidateEventTicketCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUserService,
    IUserIdentityAccessor identityAccessor,
    EventAccessService eventAccessService,
    ITicketRowLock ticketRowLock) : ICommandHandler<ValidateEventTicketCommand, ValidateEventTicketResponse>
{
    public async Task<Result<ValidateEventTicketResponse>> Handle(
        ValidateEventTicketCommand command,
        CancellationToken cancellationToken)
    {
        var gateResult = VerifiedUserGate.EnsureVerified(identityAccessor);
        if (gateResult.IsFailure)
        {
            return Result.Failure<ValidateEventTicketResponse>(gateResult.Error);
        }

        var userResult = await currentUserService.GetOrProvisionAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<ValidateEventTicketResponse>(userResult.Error);
        }

        var user = userResult.Value;

        var eventResult = await eventAccessService.GetActiveEventForDoorValidationAsync(
            command.EventId,
            cancellationToken);
        if (eventResult.IsFailure)
        {
            return Result.Failure<ValidateEventTicketResponse>(eventResult.Error);
        }

        var @event = eventResult.Value;

        var accessResult = await eventAccessService.ResolveEditAccessAsync(
            @event,
            user.Id,
            user.Id,
            cancellationToken);

        if (accessResult.IsFailure)
        {
            return Result.Failure<ValidateEventTicketResponse>(accessResult.Error);
        }

        if (@event.Status == EventStatus.Draft)
        {
            return Result.Failure<ValidateEventTicketResponse>(EventErrors.NotPublished);
        }

        if (@event.AdmissionType != AdmissionType.Paid)
        {
            return Result.Failure<ValidateEventTicketResponse>(TicketErrors.PaidAdmissionRequired);
        }

        var ticketId = await ResolveTicketIdAsync(command.Code, command.Method, cancellationToken);

        if (ticketId is null)
        {
            return Result.Success(new ValidateEventTicketResponse(TicketValidationStatus.Invalid));
        }

        var lockResult = await ticketRowLock.ExecuteAsync(
            ticketId.Value,
            (ticket, ct) =>
            {
                var validation = TicketValidationService.Validate(
                    ticket,
                    @event,
                    ticket.Validations,
                    command.Code,
                    command.Method,
                    user.Id,
                    DateTime.UtcNow);

                return Task.FromResult(
                    Result.Success(new ValidateEventTicketResponse(validation.Value.Status)));
            },
            cancellationToken);

        if (lockResult.IsFailure)
        {
            return Result.Success(new ValidateEventTicketResponse(TicketValidationStatus.Invalid));
        }

        return lockResult;
    }

    private async Task<Guid?> ResolveTicketIdAsync(
        string code,
        TicketValidationMethod method,
        CancellationToken cancellationToken)
    {
        if (method == TicketValidationMethod.QrScan)
        {
            return Guid.TryParse(code, out var ticketId) ? ticketId : null;
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        return await context.TicketCodes
            .AsNoTracking()
            .Where(tc => tc.ManualCode == normalizedCode)
            .Select(tc => (Guid?)tc.TicketId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
