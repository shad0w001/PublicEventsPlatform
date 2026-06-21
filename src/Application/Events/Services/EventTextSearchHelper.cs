using Domain.Events;
using Domain.Events.EventLocations;

namespace Application.Events.Services;

internal static class EventTextSearchHelper
{
    public static IQueryable<Event> ApplyContainsFilter(IQueryable<Event> query, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return query;
        }

        var term = searchText.Trim().ToLower();
        return query.Where(e =>
            e.Title.ToLower().Contains(term) ||
            e.Description.ToLower().Contains(term) ||
            e.LocationType.ToString().ToLower().Contains(term) ||
            e.Locations.Any(l =>
                l.Kind == EventLocationKind.Physical &&
                l.City != null &&
                l.City.Contains(term)) ||
            (e.Category != null && e.Category.Name.ToLower().Contains(term)));
    }

    public static bool MatchesText(Event @event, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        var term = searchText.Trim().ToLower();
        return @event.Title.ToLower().Contains(term) ||
               @event.Description.ToLower().Contains(term) ||
               @event.LocationType.ToString().ToLower().Contains(term) ||
               @event.Locations.Any(l =>
                   l.Kind == EventLocationKind.Physical &&
                   l.City != null &&
                   l.City.Contains(term)) ||
               (@event.Category != null && @event.Category.Name.ToLower().Contains(term));
    }
}
