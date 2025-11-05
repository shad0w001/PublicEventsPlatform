using Domain.Events;
using SharedKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Plugins
{
    public class PluginUsage : Entity
    {
        public Guid PluginId { get; set; }
        public Guid EventId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public List<PluginData> Data { get; set; } = new();

        public Plugin Plugin { get; set; }
        public Event Event { get; set; }

        }
}
