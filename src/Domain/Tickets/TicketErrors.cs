using SharedKernel;

namespace Domain.Tickets;

public static class TicketErrors
{
    public static readonly Error PriceMustBePositive = Error.Validation(
        "Tickets.PriceMustBePositive",
        "Ticket price must be greater than zero");

    public static readonly Error InvalidName = Error.Validation(
        "Tickets.InvalidName",
        "Ticket type name is required");

    public static Error NameTooLong(int maxLength) => Error.Validation(
        "Tickets.NameTooLong",
        $"Ticket type name must be at most {maxLength} characters");

    public static Error DescriptionTooLong(int maxLength) => Error.Validation(
        "Tickets.DescriptionTooLong",
        $"Ticket type description must be at most {maxLength} characters");

    public static readonly Error InvalidCapacity = Error.Validation(
        "Tickets.InvalidCapacity",
        "Ticket type capacity must be greater than zero");

    public static Error CapacityTooLow(int minimum) => Error.Validation(
        "Tickets.CapacityTooLow",
        $"Capacity cannot be less than sold plus reserved quantity ({minimum})");

    public static readonly Error PriceImmutableAfterSales = Error.Conflict(
        "Tickets.PriceImmutableAfterSales",
        "Ticket type price cannot be changed after tickets have been sold");

    public static readonly Error CannotDeleteWithSales = Error.Conflict(
        "Tickets.CannotDeleteWithSales",
        "Ticket type cannot be deleted after tickets have been sold");

    public static readonly Error CannotDeleteWithReserved = Error.Conflict(
        "Tickets.CannotDeleteWithReserved",
        "Ticket type cannot be deleted while inventory is reserved for pending orders");

    public static readonly Error InsufficientInventory = Error.Conflict(
        "Tickets.InsufficientInventory",
        "Not enough tickets available for this purchase");

    public static readonly Error InvalidQuantity = Error.Validation(
        "Tickets.InvalidQuantity",
        "Quantity must be at least one and not exceed remaining inventory");

    public static readonly Error EventNotPublished = Error.Validation(
        "Tickets.EventNotPublished",
        "Tickets can only be purchased for published events");

    public static readonly Error PaidAdmissionRequired = Error.Validation(
        "Tickets.PaidAdmissionRequired",
        "Tickets are only available on paid-admission events");

    public static readonly Error TicketTypeEventMismatch = Error.Validation(
        "Tickets.TicketTypeEventMismatch",
        "Ticket type does not belong to this event");

    public static readonly Error OrderNotPending = Error.Conflict(
        "Tickets.OrderNotPending",
        "Order is not in pending status");

    public static readonly Error OrderNotPaid = Error.Conflict(
        "Tickets.OrderNotPaid",
        "Tickets can only be issued for paid orders");

    public static readonly Error OrderQuantityMismatch = Error.Validation(
        "Tickets.OrderQuantityMismatch",
        "Order quantity does not match the reservation amount");

    public static readonly Error EventCancelled = Error.Validation(
        "Tickets.EventCancelled",
        "Tickets cannot be validated for a cancelled event");

    public static readonly Error OutsideValidationWindow = Error.Validation(
        "Tickets.OutsideValidationWindow",
        "Ticket validation is only allowed during the event time window");

    public static readonly Error InvalidCode = Error.Validation(
        "Tickets.InvalidCode",
        "The ticket code is invalid for this event");

    public static readonly Error TicketEventMismatch = Error.Validation(
        "Tickets.TicketEventMismatch",
        "The ticket does not belong to this event");

    public static readonly Error InsufficientPurchasePermissions = Error.Forbidden(
        "Tickets.InsufficientPurchasePermissions",
        "You are not authorized to buy tickets on behalf of this organization");
}
