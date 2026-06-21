using Domain.Events.EventLocations;

namespace Application.Search;

internal static class EventSearchDocumentBuilder
{
    internal sealed record Input(
        string Title,
        string Description,
        string? CategoryName,
        IReadOnlyList<EventLocation> Locations,
        string HostDisplayName,
        string? HostBioOrGroupDescription);

    internal static string Build(Input input)
    {
        var parts = new List<string>();

        AppendIfPresent(parts, input.Title);
        AppendIfPresent(parts, input.Description);
        AppendIfPresent(parts, input.CategoryName);
        AppendIfPresent(parts, input.HostDisplayName);
        AppendIfPresent(parts, input.HostBioOrGroupDescription);

        foreach (var location in input.Locations)
        {
            AppendLocation(parts, location);
        }

        return string.Join('\n', parts);
    }

    private static void AppendLocation(List<string> parts, EventLocation location)
    {
        AppendIfPresent(parts, location.Name);
        AppendIfPresent(parts, location.City);
        AppendIfPresent(parts, location.Address);
        AppendIfPresent(parts, location.Country);
        AppendIfPresent(parts, location.Url);
    }

    private static void AppendIfPresent(List<string> parts, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        parts.Add(value.Trim());
    }
}
