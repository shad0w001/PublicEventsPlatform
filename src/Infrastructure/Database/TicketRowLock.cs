using Application.Abstractions.Data;
using Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Infrastructure.Database;

internal sealed class TicketRowLock(ApplicationDbContext context) : ITicketRowLock
{
    public async Task<Result<T>> ExecuteAsync<T>(
        Guid ticketId,
        Func<Ticket, CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken)
    {
        if (context.Database.IsNpgsql())
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);

            var result = await ExecuteCoreAsync(ticketId, work, cancellationToken);

            if (result.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }

        var coreResult = await ExecuteCoreAsync(ticketId, work, cancellationToken);

        if (coreResult.IsFailure)
        {
            return coreResult;
        }

        await context.SaveChangesAsync(cancellationToken);
        return coreResult;
    }

    private async Task<Result<T>> ExecuteCoreAsync<T>(
        Guid ticketId,
        Func<Ticket, CancellationToken, Task<Result<T>>> work,
        CancellationToken cancellationToken)
    {
        Ticket? ticket = await LockTicketAsync(ticketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<T>(TicketErrors.TicketNotFound(ticketId));
        }

        var existingValidationIds = ticket.Validations.Select(v => v.Id).ToHashSet();

        var workResult = await work(ticket, cancellationToken);

        if (workResult.IsFailure)
        {
            return workResult;
        }

        AttachNewValidations(ticket, existingValidationIds);
        DiscardUnrelatedPendingChanges();

        return workResult;
    }

    private void AttachNewValidations(Ticket ticket, HashSet<Guid> existingValidationIds)
    {
        foreach (var validation in ticket.Validations.Where(v => !existingValidationIds.Contains(v.Id)))
        {
            if (context.Entry(validation).State == EntityState.Detached)
            {
                context.TicketValidations.Add(validation);
            }
        }
    }

    private void DiscardUnrelatedPendingChanges()
    {
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State == EntityState.Unchanged)
            {
                continue;
            }

            if (entry.Entity is TicketValidation && entry.State == EntityState.Added)
            {
                continue;
            }

            entry.State = EntityState.Unchanged;
        }
    }

    private async Task<Ticket?> LockTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        if (context.Database.IsNpgsql())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT "Id" FROM tickets WHERE "Id" = {ticketId} FOR UPDATE""",
                cancellationToken);
        }

        return await context.Tickets
            .Include(t => t.TicketCode)
            .Include(t => t.Validations)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
    }
}
