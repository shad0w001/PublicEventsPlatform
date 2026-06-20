using System.Security.Cryptography;
using SharedKernel;

namespace Domain.Tickets.Services;

public static class TicketService
{
    public static Result<IReadOnlyList<Ticket>> IssueTickets(
        Order order,
        TicketType ticketType,
        Guid participantId,
        int quantity,
        Func<string, bool>? isManualCodeTaken = null)
    {
        if (order.Status != OrderStatus.Paid)
        {
            return Result.Failure<IReadOnlyList<Ticket>>(TicketErrors.OrderNotPaid);
        }

        if (order.Quantity != quantity || order.TicketTypeId != ticketType.Id || order.ParticipantId != participantId)
        {
            return Result.Failure<IReadOnlyList<Ticket>>(TicketErrors.OrderQuantityMismatch);
        }

        var tickets = new List<Ticket>(quantity);

        for (var i = 0; i < quantity; i++)
        {
            var ticket = new Ticket
            {
                OrderId = order.Id,
                TicketTypeId = ticketType.Id,
                EventId = order.EventId,
                ParticipantId = participantId,
                Order = order,
                TicketType = ticketType
            };

            var manualCode = GenerateUniqueManualCode(isManualCodeTaken);
            ticket.TicketCode = new TicketCode
            {
                TicketId = ticket.Id,
                ManualCode = manualCode,
                Ticket = ticket
            };

            order.Tickets.Add(ticket);
            ticketType.Tickets.Add(ticket);
            tickets.Add(ticket);
        }

        return tickets;
    }

    public static string GenerateManualCode()
    {
        Span<char> buffer = stackalloc char[TicketConstants.ManualCodeLength];
        var alphabet = TicketConstants.ManualCodeAlphabet;

        for (var i = 0; i < TicketConstants.ManualCodeLength; i++)
        {
            var index = RandomNumberGenerator.GetInt32(alphabet.Length);
            buffer[i] = alphabet[index];
        }

        return new string(buffer);
    }

    private static string GenerateUniqueManualCode(Func<string, bool>? isManualCodeTaken)
    {
        const int maxAttempts = 20;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateManualCode();

            if (isManualCodeTaken is null || !isManualCodeTaken(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not generate a unique manual ticket code.");
    }
}
