using SharedKernel;

namespace Domain.Plugins;

public class Plugin : Entity
{
    public string Code { get; internal set; } = string.Empty;
    public string Name { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public string Version { get; internal set; } = string.Empty;

    public List<PluginUsage> Usages { get; internal set; } = [];
}
