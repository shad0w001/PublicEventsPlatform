using Application.Abstractions.Data;
using Application.Abstractions.Search;
using Application.Events.Services;
using Domain.Events;
using Domain.Search;
using Domain.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Search.Services;

internal sealed class EventEmbeddingIndexService(
    IApplicationDbContext context,
    IEmbeddingGenerator embeddingGenerator,
    TimeProvider timeProvider,
    ILogger<EventEmbeddingIndexService> logger) : IEventEmbeddingIndexService
{
    public async Task UpsertAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var eventData = await LoadEventForIndexingAsync(eventId, cancellationToken);
        if (eventData is null)
        {
            logger.LogWarning(
                "Skipping embedding upsert for event {EventId}: not found or not published",
                eventId);
            return;
        }

        var document = EventSearchDocumentBuilder.Build(eventData.DocumentInput);
        if (string.IsNullOrWhiteSpace(document))
        {
            logger.LogWarning(
                "Skipping embedding upsert for event {EventId}: empty search document",
                eventId);
            return;
        }

        float[] embedding;
        try
        {
            embedding = await embeddingGenerator.GenerateAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Embedding generation failed for event {EventId}",
                eventId);
            throw;
        }

        var dimensionValidation = EventEmbeddingService.ValidateDimensions(embedding);
        if (dimensionValidation.IsFailure)
        {
            logger.LogError(
                "Invalid embedding dimensions for event {EventId}: {Error}",
                eventId,
                dimensionValidation.Error.Message);
            throw new InvalidOperationException(dimensionValidation.Error.Message);
        }

        var updatedAt = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await context.EventEmbeddings
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);

        if (existing is null)
        {
            var createResult = EventEmbeddingService.Create(eventId, embedding, updatedAt);
            if (createResult.IsFailure)
            {
                throw new InvalidOperationException(createResult.Error.Message);
            }

            context.EventEmbeddings.Add(createResult.Value);
        }
        else
        {
            var updateResult = EventEmbeddingService.Update(existing, embedding, updatedAt);
            if (updateResult.IsFailure)
            {
                throw new InvalidOperationException(updateResult.Error.Message);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Upserted embedding for event {EventId}",
            eventId);
    }

    public async Task DeleteAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var existing = await context.EventEmbeddings
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);

        if (existing is null)
        {
            logger.LogDebug(
                "No embedding row to delete for event {EventId}",
                eventId);
            return;
        }

        context.EventEmbeddings.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Deleted embedding for event {EventId}",
            eventId);
    }

    private async Task<LoadedEventData?> LoadEventForIndexingAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var @event = await context.Events
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Locations)
            .Include(e => e.Organizers)
            .FirstOrDefaultAsync(
                e => e.Id == eventId &&
                     e.DeletedAt == null &&
                     e.Status == EventStatus.Published,
                cancellationToken);

        if (@event is null)
        {
            return null;
        }

        var hostParticipantId = @event.Organizers.FirstOrDefault()?.ParticipantId;
        if (hostParticipantId is null)
        {
            return new LoadedEventData(new EventSearchDocumentBuilder.Input(
                @event.Title,
                @event.Description,
                @event.Category?.Name,
                @event.Locations,
                HostDisplayName: "Host",
                HostBioOrGroupDescription: null));
        }

        var hostIsGroup = await context.Groups
            .AsNoTracking()
            .AnyAsync(g => g.Id == hostParticipantId.Value, cancellationToken);

        var hostDisplayName = await EventHostDisplayNameLookup.ResolveAsync(
            context,
            hostParticipantId.Value,
            hostIsGroup,
            cancellationToken);

        string? hostBioOrGroupDescription = null;
        if (hostIsGroup)
        {
            hostBioOrGroupDescription = await context.Groups
                .AsNoTracking()
                .Where(g => g.Id == hostParticipantId.Value)
                .Select(g => g.Description)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            hostBioOrGroupDescription = await context.Users
                .AsNoTracking()
                .Where(u => u.Id == hostParticipantId.Value)
                .Select(u => u.Bio)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new LoadedEventData(new EventSearchDocumentBuilder.Input(
            @event.Title,
            @event.Description,
            @event.Category?.Name,
            @event.Locations,
            hostDisplayName,
            hostBioOrGroupDescription));
    }

    internal async Task<string?> BuildSearchDocumentAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var eventData = await LoadEventForIndexingAsync(eventId, cancellationToken);
        return eventData is null
            ? null
            : EventSearchDocumentBuilder.Build(eventData.DocumentInput);
    }

    private sealed record LoadedEventData(EventSearchDocumentBuilder.Input DocumentInput);
}
