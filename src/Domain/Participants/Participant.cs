using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Events;
using SharedKernel;

namespace Domain.Participants
{
    public abstract class Participant : Entity
    {
        public ParticipantType Type { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<EventOrganizer> OrganizedEvents { get; set; } = new List<Events.EventOrganizer>();
        public ICollection<EventAttendee> AttendedEvents { get; set; } = new List<Events.EventAttendee>();
    }
}
