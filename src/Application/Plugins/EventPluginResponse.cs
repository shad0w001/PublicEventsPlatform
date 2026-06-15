namespace Application.Plugins;

public sealed record EventPluginResponse(
    Guid PluginId,
    string Code,
    string Name,
    IReadOnlyDictionary<string, string?> Data,
    DateTime AttachedAt);
