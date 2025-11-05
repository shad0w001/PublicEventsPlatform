using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Plugins
{
    public class PluginUsage
    {
        public Guid PluginId { get; set; }
        public Guid EventId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public List<PluginData> Data { get; set; }

        public Plugin Plugin { get; set; }
    }
}
