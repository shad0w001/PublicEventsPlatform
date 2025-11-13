using Domain.Groups;
using Domain.Participants;
using SharedKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Domain.Users
{
    public class User : Participant
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string? Bio { get; set; }
        public DateTime LastActive { get; set; }
        public List<GroupMembership> GroupMemberships { get; set; } = new List<GroupMembership>();
    }
}
