using Application.Abstractions.Media;
using Application.Abstractions.Messaging;
using Application.Events;

namespace Application.Events.UploadEventBanner;

public sealed record UploadEventBannerCommand(
    Guid EventId,
    MediaUploadRequest Upload) : ICommand<EventDetailResponse>;
