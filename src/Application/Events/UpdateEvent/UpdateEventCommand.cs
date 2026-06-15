using Application.Abstractions.Messaging;
using Domain.Events;

namespace Application.Events.UpdateEvent;

public sealed record UpdateEventCommand(
    Guid EventId,
    string? Title = null,
    string? Description = null,
    Guid? CategoryId = null,
    string? BannerImageUrl = null,
    EventTier? Tier = null,
    IReadOnlyList<EventLocationResponse>? Locations = null,
    DateTime? StartTime = null,
    DateTime? EndTime = null,
    string? TimeZoneId = null,
    AdmissionType? AdmissionType = null) : ICommand<EventDetailResponse>;
