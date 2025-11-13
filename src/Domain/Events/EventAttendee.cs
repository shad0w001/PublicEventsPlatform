using Domain.Participants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Events
{
    public class EventAttendee
    {
        public Guid EventId { get; set; }
        public Guid ParticipantId { get; set; }

        public Event Event { get; set; }
        public Participant Attendee { get; set; }

        public DateTime? RegisteredAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public EventAttendeeStatus Status { get; set; }
    }
}
