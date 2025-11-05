using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedKernel
{
    public abstract class Entity
    {
        public Guid Id { get; private set; } = Guid.CreateVersion7();
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    }
}
