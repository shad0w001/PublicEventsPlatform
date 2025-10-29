using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Event
{
    public class EventLocation
    {
        public EventLocationType LocationType { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public DateTime Date { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Url { get; set; }
    }
}
