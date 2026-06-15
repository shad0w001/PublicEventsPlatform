using System.Text.Json;
using Domain.Events;
using SharedKernel;

namespace Domain.Plugins.Services;

public static class PluginService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Result ValidatePluginData(
        string code,
        IReadOnlyDictionary<string, string?> data,
        DateTime eventStart,
        DateTime eventEnd)
    {
        if (data.Count == 0)
        {
            return Result.Failure(PluginErrors.DataRequired);
        }

        return code switch
        {
            PluginConstants.CodeAgenda => ValidateAgendaData(data, eventStart, eventEnd),
            PluginConstants.CodeFaq => ValidateFaqData(data),
            PluginConstants.CodeLinks => ValidateLinksData(data),
            _ => Result.Failure(PluginErrors.UnknownCode(code))
        };
    }

    public static Result<PluginUsage> Attach(
        Event @event,
        Plugin catalog,
        IReadOnlyDictionary<string, string?> data)
    {
        if (@event.Tier != EventTier.Big)
        {
            return Result.Failure<PluginUsage>(PluginErrors.BigTierRequired);
        }

        if (@event.Plugins.Count >= PluginConstants.MaxPluginsPerEvent)
        {
            return Result.Failure<PluginUsage>(
                PluginErrors.MaxPluginsExceeded(PluginConstants.MaxPluginsPerEvent));
        }

        if (@event.Plugins.Any(u => u.PluginId == catalog.Id))
        {
            return Result.Failure<PluginUsage>(PluginErrors.AlreadyAttached);
        }

        var validationResult = ValidatePluginData(
            catalog.Code,
            data,
            @event.StartTime,
            @event.EndTime);

        if (validationResult.IsFailure)
        {
            return Result.Failure<PluginUsage>(validationResult.Error);
        }

        var usage = new PluginUsage
        {
            PluginId = catalog.Id,
            IsActive = true,
            Event = @event
        };

        ApplyDataRows(usage, data);
        @event.Plugins.Add(usage);

        return usage;
    }

    public static Result UpdateConfig(
        PluginUsage usage,
        string code,
        IReadOnlyDictionary<string, string?> data,
        DateTime eventStart,
        DateTime eventEnd)
    {
        var validationResult = ValidatePluginData(code, data, eventStart, eventEnd);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        var existingByKey = usage.Data.ToDictionary(d => d.Key);

        foreach (var (key, value) in data)
        {
            if (existingByKey.TryGetValue(key, out var row))
            {
                row.Value = value;
            }
            else
            {
                usage.Data.Add(new PluginData
                {
                    Key = key,
                    Value = value,
                    PluginUsage = usage
                });
            }
        }

        foreach (var row in usage.Data.Where(d => !data.ContainsKey(d.Key)).ToList())
        {
            usage.Data.Remove(row);
        }

        return Result.Success();
    }

    public static Result Detach(Event @event, Guid pluginId)
    {
        var usage = @event.Plugins.FirstOrDefault(u => u.PluginId == pluginId);
        if (usage is null)
        {
            return Result.Failure(PluginErrors.NotAttached(pluginId));
        }

        @event.Plugins.Remove(usage);
        return Result.Success();
    }

    public static void DetachAll(Event @event)
    {
        @event.Plugins.Clear();
    }

    private static void ApplyDataRows(PluginUsage usage, IReadOnlyDictionary<string, string?> data)
    {
        foreach (var (key, value) in data)
        {
            usage.Data.Add(new PluginData
            {
                Key = key,
                Value = value,
                PluginUsage = usage
            });
        }
    }

    private static Result ValidateAgendaData(
        IReadOnlyDictionary<string, string?> data,
        DateTime eventStart,
        DateTime eventEnd)
    {
        if (!data.TryGetValue(PluginConstants.DataKeySessions, out var sessionsJson) ||
            string.IsNullOrWhiteSpace(sessionsJson))
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        AgendaSessionDto[] sessions;
        try
        {
            sessions = JsonSerializer.Deserialize<AgendaSessionDto[]>(sessionsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        if (sessions.Length == 0)
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        foreach (var session in sessions)
        {
            if (string.IsNullOrWhiteSpace(session.Title) ||
                session.Title.Length > PluginConstants.AgendaTitleMaxLength ||
                string.IsNullOrWhiteSpace(session.Description) ||
                session.Description.Length > PluginConstants.AgendaDescriptionMaxLength)
            {
                return Result.Failure(PluginErrors.InvalidData);
            }

            if (!session.Start.HasValue || !session.End.HasValue ||
                session.Start.Value >= session.End.Value)
            {
                return Result.Failure(PluginErrors.InvalidData);
            }

            if (session.Start.Value < eventStart || session.End.Value > eventEnd)
            {
                return Result.Failure(PluginErrors.InvalidData);
            }

            if (session.Speaker is not null &&
                (string.IsNullOrWhiteSpace(session.Speaker) ||
                 session.Speaker.Length > PluginConstants.AgendaSpeakerMaxLength))
            {
                return Result.Failure(PluginErrors.InvalidData);
            }
        }

        return Result.Success();
    }

    private static Result ValidateFaqData(IReadOnlyDictionary<string, string?> data)
    {
        if (!data.TryGetValue(PluginConstants.DataKeyEntries, out var entriesJson) ||
            string.IsNullOrWhiteSpace(entriesJson))
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        FaqEntryDto[] entries;
        try
        {
            entries = JsonSerializer.Deserialize<FaqEntryDto[]>(entriesJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        if (entries.Length == 0)
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Question) ||
                entry.Question.Length > PluginConstants.FaqQuestionMaxLength ||
                string.IsNullOrWhiteSpace(entry.Answer) ||
                entry.Answer.Length > PluginConstants.FaqAnswerMaxLength)
            {
                return Result.Failure(PluginErrors.InvalidData);
            }
        }

        return Result.Success();
    }

    private static Result ValidateLinksData(IReadOnlyDictionary<string, string?> data)
    {
        if (!data.TryGetValue(PluginConstants.DataKeyLinks, out var linksJson) ||
            string.IsNullOrWhiteSpace(linksJson))
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        LinkItemDto[] links;
        try
        {
            links = JsonSerializer.Deserialize<LinkItemDto[]>(linksJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        if (links.Length == 0)
        {
            return Result.Failure(PluginErrors.InvalidData);
        }

        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link.Label) ||
                link.Label.Length > PluginConstants.LinkLabelMaxLength)
            {
                return Result.Failure(PluginErrors.InvalidData);
            }

            if (string.IsNullOrWhiteSpace(link.Url) ||
                link.Url.Length > PluginConstants.LinkUrlMaxLength ||
                !Uri.TryCreate(link.Url, UriKind.Absolute, out _))
            {
                return Result.Failure(PluginErrors.InvalidData);
            }

            if (link.Description is not null &&
                (string.IsNullOrWhiteSpace(link.Description) ||
                 link.Description.Length > PluginConstants.LinkDescriptionMaxLength))
            {
                return Result.Failure(PluginErrors.InvalidData);
            }
        }

        return Result.Success();
    }

    private sealed class AgendaSessionDto
    {
        public string? Title { get; init; }
        public string? Description { get; init; }
        public DateTime? Start { get; init; }
        public DateTime? End { get; init; }
        public string? Speaker { get; init; }
    }

    private sealed class FaqEntryDto
    {
        public string? Question { get; init; }
        public string? Answer { get; init; }
    }

    private sealed class LinkItemDto
    {
        public string? Label { get; init; }
        public string? Url { get; init; }
        public string? Description { get; init; }
    }
}
