using SharedKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Group
{
    public class Group : Entity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string? ProfileImageUrl { get; set; }
        public List<GroupMember> GroupMembers { get; set; }
    }
}
