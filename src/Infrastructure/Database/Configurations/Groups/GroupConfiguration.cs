using Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Groups;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(GroupConstants.NameMaxLength);

        builder.Property(g => g.Description)
            .IsRequired()
            .HasMaxLength(GroupConstants.DescriptionMaxLength)
            .HasDefaultValue(string.Empty);

        builder.Property(g => g.ProfileImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(g => g.JoinPolicy)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(GroupJoinPolicy.Open);

        builder.Property(g => g.DeletedAt);
    }
}
