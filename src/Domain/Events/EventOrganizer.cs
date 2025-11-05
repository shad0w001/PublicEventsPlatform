using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Events
{
    public class EventOrganizer
    {
        public Guid EventId { get; set; }
        public Guid ActorId { get; set; }
    }
}
