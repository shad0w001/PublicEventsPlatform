using Domain.Tickets;
using SharedKernel;

namespace Application.Abstractions.Data;

public interface ITicketTypeRowLock
{
    Task<Result<T>> ExecuteAsync<T>(
        Guid ticketTypeId,
        Func<TicketType, CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken);
}
