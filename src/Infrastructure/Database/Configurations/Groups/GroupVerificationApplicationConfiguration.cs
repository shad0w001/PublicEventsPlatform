using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Groups;

public class GroupVerificationApplicationConfiguration : IEntityTypeConfiguration<GroupVerificationApplication>
{
    public void Configure(EntityTypeBuilder<GroupVerificationApplication> builder)
    {
        builder.ToTable("group_verification_applications");

        builder.Property(a => a.GroupId)
            .IsRequired();

        builder.Property(a => a.SubmittedByUserId)
            .IsRequired();

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(a => a.SubmittedAt)
            .IsRequired();

        builder.HasOne(a => a.Group)
            .WithMany(g => g.VerificationApplications)
            .HasForeignKey(a => a.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.SubmittedByUser)
            .WithMany()
            .HasForeignKey(a => a.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.GroupId)
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
    }
}
