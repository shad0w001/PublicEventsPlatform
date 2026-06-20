using Domain.Tickets;
using SharedKernel;

namespace Application.Abstractions.Data;

public interface ITicketRowLock
{
    Task<Result<T>> ExecuteAsync<T>(
        Guid ticketId,
        Func<Ticket, CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken);
}
