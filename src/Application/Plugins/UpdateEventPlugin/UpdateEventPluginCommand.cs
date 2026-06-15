using Application.Abstractions.Messaging;
using Application.Plugins;

namespace Application.Plugins.UpdateEventPlugin;

public sealed record UpdateEventPluginCommand(
    Guid EventId,
    Guid PluginId,
    IReadOnlyDictionary<string, string?> Data) : ICommand<EventPluginResponse>;
