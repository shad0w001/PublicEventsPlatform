using Application.Abstractions.Messaging;

namespace Application.Plugins.DetachEventPlugin;

public sealed record DetachEventPluginCommand(Guid EventId, Guid PluginId) : ICommand;
