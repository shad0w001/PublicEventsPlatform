using SharedKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Plugins
{
    public class PluginData : Entity
    {
        public Guid PluginId { get; set; }
        public string Key { get; set; }
        public string? Value { get; set; }

        public Plugin Plugin { get; set; }
    }
}
