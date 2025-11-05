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
        public Guid AttendeeId { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public EventAttendeeStatus Status { get; set; }
    }
}
