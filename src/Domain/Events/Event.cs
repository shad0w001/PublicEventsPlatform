using Domain.Actors;
using Domain.Events.EventLocations;
using Domain.Plugins;
using SharedKernel;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Events
{
    public class Event : Entity
    {
        public string Title { get; set; }
        public Guid? CategoryId { get; set; }
        public string Description { get; set; }
        public string? BannerImageUrl { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public EventStatus Status { get; set; }
        public EventLocationType LocationType { get; set; }

        public EventCategory? Category { get; set; }
        public List<EventLocation> Locations { get; set; }
        public List<EventOrganizer> Organizers { get; set; }
        public List<EventAttendee> Attendees { get; set; }
        public List<PluginUsage> Plugins { get; set; } = new();
    }
}
