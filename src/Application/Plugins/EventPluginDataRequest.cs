namespace Application.Plugins;

public sealed record EventPluginDataRequest(IReadOnlyDictionary<string, string?> Data);
