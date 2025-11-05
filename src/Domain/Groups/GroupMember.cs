using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Users;

namespace Domain.Groups
{
    public class GroupMember
    {
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public DateTime JoinedAt { get; set; }
        public GroupMemberRole Role { get; set; }

        public Group Group { get; set; }
        public User User { get; set; }
    }

}
