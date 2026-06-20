using Domain.Events;
using Domain.Tickets;
using SharedKernel;

namespace Domain.Participants
{
    public abstract class Participant : Entity
    {
        public ICollection<EventOrganizer> OrganizedEvents { get; set; } = new List<EventOrganizer>();
        public ICollection<EventAttendee> AttendedEvents { get; set; } = new List<EventAttendee>();
        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
