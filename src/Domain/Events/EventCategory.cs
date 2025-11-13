using SharedKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Events
{
    public class EventCategory : Entity
    {
        public string Name { get; set; } = string.Empty;

        public Guid? ParentCategoryId { get; set; }
        public EventCategory? ParentCategory { get; set; }
        public List<EventCategory> Subcategories { get; set; } = new();

        public List<Event> Events { get; set; } = new();
    }
}
