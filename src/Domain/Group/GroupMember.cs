using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Group
{
    public class GroupMember
    {
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public DateTime JoinedAt { get; set; }
        public GroupMemberRole Role { get; set; }
    }

}
