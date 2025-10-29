using Domain.Group;
using SharedKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.User
{
    public class User : Entity
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string? Bio { get; set; }
        public DateTime LastActive { get; set; }
        public List<GroupMembership> Groups { get; set; }
    }
}
