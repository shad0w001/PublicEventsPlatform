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
    public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMembership>
    {
        public void Configure(EntityTypeBuilder<GroupMembership> builder)
        {
            builder.ToTable("group_memberships");

            builder.HasKey(gm => new { gm.GroupId, gm.UserId });

            builder.Property(gm => gm.JoinedAt)
                .IsRequired();

            builder.HasOne(gm => gm.Group)
            .WithMany(g => g.GroupMemberships)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(gm => gm.User)
                .WithMany(u => u.GroupMemberships)
                .HasForeignKey(gm => gm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            //this is an enum
            builder.Property(gm => gm.Role)
                .IsRequired()
                .HasConversion<string>();
        }
    }
}
