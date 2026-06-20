using Domain.Events;
using SharedKernel;

namespace Domain.Tickets.Services;

public static class TicketValidationService
{
    public static Result<TicketValidation> Validate(
        Ticket ticket,
        Event @event,
        IReadOnlyList<TicketValidation> validations,
        string code,
        TicketValidationMethod method,
        Guid validatedByUserId,
        DateTime utcNow)
    {
        if (ticket.EventId != @event.Id)
        {
            return Result.Success(CreateAuditRow(
                ticket,
                validatedByUserId,
                method,
                TicketValidationStatus.Invalid));
        }

        var status = DetermineStatus(ticket, @event, validations, code, method, utcNow);

        return Result.Success(CreateAuditRow(
            ticket,
            validatedByUserId,
            method,
            status));
    }

    private static TicketValidationStatus DetermineStatus(
        Ticket ticket,
        Event @event,
        IReadOnlyList<TicketValidation> validations,
        string code,
        TicketValidationMethod method,
        DateTime utcNow)
    {
        if (@event.Status == EventStatus.Cancelled)
        {
            return TicketValidationStatus.Invalid;
        }

        if (utcNow < @event.StartTime || utcNow > @event.EndTime)
        {
            return TicketValidationStatus.Invalid;
        }

        if (!CodeMatches(ticket, code, method))
        {
            return TicketValidationStatus.Invalid;
        }

        if (validations.Any(v => v.Status == TicketValidationStatus.Valid))
        {
            return TicketValidationStatus.AlreadyUsed;
        }

        return TicketValidationStatus.Valid;
    }

    private static bool CodeMatches(Ticket ticket, string code, TicketValidationMethod method) =>
        method switch
        {
            TicketValidationMethod.QrScan =>
                Guid.TryParse(code, out var ticketId) && ticketId == ticket.Id,
            TicketValidationMethod.ManualEntry =>
                ticket.TicketCode is not null &&
                string.Equals(ticket.TicketCode.ManualCode, code.Trim(), StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static TicketValidation CreateAuditRow(
        Ticket ticket,
        Guid validatedByUserId,
        TicketValidationMethod method,
        TicketValidationStatus status)
    {
        var validation = new TicketValidation
        {
            TicketId = ticket.Id,
            ValidatedByUserId = validatedByUserId,
            Method = method,
            Status = status,
            Ticket = ticket
        };

        ticket.Validations.Add(validation);
        return validation;
    }
}
