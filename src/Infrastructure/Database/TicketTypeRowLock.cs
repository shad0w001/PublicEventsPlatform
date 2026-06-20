using Application.Abstractions.Data;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Infrastructure.Database;

internal sealed class TicketTypeRowLock(ApplicationDbContext context) : ITicketTypeRowLock
{
    public async Task<Result<T>> ExecuteAsync<T>(
        Guid ticketTypeId,
        Func<TicketType, CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        TicketType? ticketType = await LockTicketTypeAsync(ticketTypeId, cancellationToken);

        if (ticketType is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<T>(TicketErrors.TicketTypeNotFound(ticketTypeId));
        }

        var workResult = await work(ticketType, cancellationToken);

        if (workResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return workResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return workResult;
    }

    private async Task<TicketType?> LockTicketTypeAsync(
        Guid ticketTypeId,
        CancellationToken cancellationToken)
    {
        if (context.Database.IsNpgsql())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT "Id" FROM ticket_types WHERE "Id" = {ticketTypeId} FOR UPDATE""",
                cancellationToken);
        }

        return await context.TicketTypes
            .Include(t => t.Event)
            .FirstOrDefaultAsync(t => t.Id == ticketTypeId, cancellationToken);
    }
}
