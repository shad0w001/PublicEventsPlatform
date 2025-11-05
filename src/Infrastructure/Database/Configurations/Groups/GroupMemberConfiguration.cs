using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Database.Configurations.Groups
{
    public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
    {
        public void Configure(EntityTypeBuilder<GroupMember> builder)
        {
            builder.ToTable("group_members");

            builder.HasKey(gm => new { gm.GroupId, gm.UserId });

            builder.Property(gm => gm.JoinedAt)
                .IsRequired();

            //this is an enum
            builder.Property(gm => gm.Role)
                .IsRequired()
                .HasConversion<string>();
        }
    }
}
