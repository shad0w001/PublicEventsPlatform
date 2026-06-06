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
        public ICollection<EventOrganizer> OrganizedEvents { get; set; } = new List<EventOrganizer>();
        public ICollection<EventAttendee> AttendedEvents { get; set; } = new List<EventAttendee>();
    }
}
