using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Groups;

public class GroupJoinApplicationConfiguration : IEntityTypeConfiguration<GroupJoinApplication>
{
    public void Configure(EntityTypeBuilder<GroupJoinApplication> builder)
    {
        builder.ToTable("group_join_applications");

        builder.Property(a => a.GroupId)
            .IsRequired();

        builder.Property(a => a.UserId)
            .IsRequired();

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.SubmittedAt)
            .IsRequired();

        builder.HasOne(a => a.Group)
            .WithMany(g => g.JoinApplications)
            .HasForeignKey(a => a.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.GroupId, a.UserId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
    }
}
