using Domain.Events;
using Domain.Subscriptions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Subscriptions;

public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("user_subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.Kind)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.City)
            .HasMaxLength(SubscriptionConstants.CityMaxLength);

        builder.HasIndex(s => s.UserId);

        builder.HasIndex(s => new { s.UserId, s.City })
            .IsUnique()
            .HasFilter("\"Kind\" = 'City'");

        builder.HasIndex(s => new { s.UserId, s.CategoryId })
            .IsUnique()
            .HasFilter("\"Kind\" = 'Category'");

        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter("\"Kind\" = 'Online'");

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<EventCategory>()
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
