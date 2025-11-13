using Domain.Participants;
using SharedKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Groups
{
    public class Group : Participant
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string? ProfileImageUrl { get; set; }
        public List<GroupMembership> GroupMemberships { get; set; } = new List<GroupMembership>();
    }
}
