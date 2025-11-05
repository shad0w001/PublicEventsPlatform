using Domain.Events.EventLocations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Events.EventLocations
{
    public class EventLocation
    {
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public EventLocationKind Kind { get; set; }

        public string? Url { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? ExternalPlaceId { get; set; }
    }
}
