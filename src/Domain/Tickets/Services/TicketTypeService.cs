using Domain.Events;
using SharedKernel;

namespace Domain.Tickets.Services;

public static class TicketTypeService
{
    public static Result<TicketType> Create(
        Event @event,
        string name,
        string? description,
        int priceCents,
        int capacity)
    {
        var nameResult = ValidateName(name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<TicketType>(nameResult.Error);
        }

        var descriptionResult = ValidateDescription(description);
        if (descriptionResult.IsFailure)
        {
            return Result.Failure<TicketType>(descriptionResult.Error);
        }

        if (priceCents <= 0)
        {
            return Result.Failure<TicketType>(TicketErrors.PriceMustBePositive);
        }

        if (capacity <= 0)
        {
            return Result.Failure<TicketType>(TicketErrors.InvalidCapacity);
        }

        var ticketType = new TicketType
        {
            EventId = @event.Id,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            PriceCents = priceCents,
            Capacity = capacity,
            SoldQuantity = 0,
            ReservedQuantity = 0,
            Event = @event
        };

        @event.TicketTypes.Add(ticketType);
        return ticketType;
    }

    public static Result Update(
        TicketType ticketType,
        string? name,
        string? description,
        int? capacity)
    {
        if (name is not null)
        {
            var nameResult = ValidateName(name);
            if (nameResult.IsFailure)
            {
                return nameResult;
            }

            ticketType.Name = name.Trim();
        }

        if (description is not null)
        {
            var descriptionResult = ValidateDescription(description);
            if (descriptionResult.IsFailure)
            {
                return descriptionResult;
            }

            ticketType.Description = description.Trim();
        }

        if (capacity is not null)
        {
            if (capacity.Value <= 0)
            {
                return Result.Failure(TicketErrors.InvalidCapacity);
            }

            var minimumCapacity = ticketType.SoldQuantity + ticketType.ReservedQuantity;
            if (capacity.Value < minimumCapacity)
            {
                return Result.Failure(TicketErrors.CapacityTooLow(minimumCapacity));
            }

            ticketType.Capacity = capacity.Value;
        }

        return Result.Success();
    }

    public static Result Delete(TicketType ticketType)
    {
        if (ticketType.SoldQuantity > 0)
        {
            return Result.Failure(TicketErrors.CannotDeleteWithSales);
        }

        if (ticketType.ReservedQuantity > 0)
        {
            return Result.Failure(TicketErrors.CannotDeleteWithReserved);
        }

        ticketType.Event.TicketTypes.Remove(ticketType);
        return Result.Success();
    }

    public static int GetRemainingQuantity(TicketType ticketType) =>
        ticketType.Capacity - ticketType.SoldQuantity - ticketType.ReservedQuantity;

    internal static Result ValidateName(string name)
    {
        var trimmed = name.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(TicketErrors.InvalidName);
        }

        if (trimmed.Length > TicketConstants.NameMaxLength)
        {
            return Result.Failure(TicketErrors.NameTooLong(TicketConstants.NameMaxLength));
        }

        return Result.Success();
    }

    internal static Result ValidateDescription(string? description)
    {
        if (description is null)
        {
            return Result.Success();
        }

        var trimmed = description.Trim();

        if (trimmed.Length > TicketConstants.DescriptionMaxLength)
        {
            return Result.Failure(TicketErrors.DescriptionTooLong(TicketConstants.DescriptionMaxLength));
        }

        return Result.Success();
    }
}
