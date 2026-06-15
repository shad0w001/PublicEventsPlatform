using Application.Abstractions.Messaging;
using Application.Plugins;

namespace Application.Plugins.AttachEventPlugin;

public sealed record AttachEventPluginCommand(
    Guid EventId,
    Guid PluginId,
    IReadOnlyDictionary<string, string?> Data) : ICommand<EventPluginResponse>;
