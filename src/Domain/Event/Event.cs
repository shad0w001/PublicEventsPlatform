using Domain.Actor;
using SharedKernel;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Event
{
    public class Event : Entity
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string? BannerImageUrl { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<EventLocation> Locations { get; set; }
        public bool IsOnline { get; set; }
        public bool IsHybrid { get; set; }
        public List<EventOrganizer> Organizers { get; set; }
        public List<EventAttendee> Attendees { get; set; }
        public EventStatus Status { get; set; }
    }
}
