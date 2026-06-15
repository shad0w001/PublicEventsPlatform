using Domain.Events;
using SharedKernel;

namespace Domain.Plugins;

public static class PluginErrors
{
    public static Error NotFound(Guid pluginId) => Error.NotFound(
        "Plugins.NotFound",
        $"The plugin with the Id = '{pluginId}' was not found");

    public static Error NotAttached(Guid pluginId) => Error.NotFound(
        "Plugins.NotAttached",
        $"Plugin '{pluginId}' is not attached to this event");

    public static Error UnknownCode(string code) => Error.Validation(
        "Plugins.UnknownCode",
        $"Unknown plugin code '{code}'");

    public static readonly Error DataRequired = Error.Validation(
        "Plugins.DataRequired",
        "Plugin data must include at least one key");

    public static readonly Error InvalidData = Error.Validation(
        "Plugins.InvalidData",
        "Plugin data is invalid for this plugin type");

    public static readonly Error BigTierRequired = Error.Validation(
        "Plugins.BigTierRequired",
        "Plugins are only available on big-tier events");

    public static Error MaxPluginsExceeded(int max) => Error.Validation(
        "Plugins.MaxPluginsExceeded",
        $"Events may have at most {max} plugins");

    public static readonly Error AlreadyAttached = Error.Conflict(
        "Plugins.AlreadyAttached",
        "This plugin is already attached to the event");
}
