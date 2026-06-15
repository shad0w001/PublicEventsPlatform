using Domain.Events;
using Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Plugins;

public class PluginUsageConfiguration : IEntityTypeConfiguration<PluginUsage>
{
    public void Configure(EntityTypeBuilder<PluginUsage> builder)
    {
        builder.ToTable("plugin_usages");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.IsActive)
            .IsRequired();

        builder.HasOne(u => u.Plugin)
            .WithMany(p => p.Usages)
            .HasForeignKey(u => u.PluginId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Event)
            .WithMany(e => e.Plugins)
            .HasForeignKey(u => u.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Data)
            .WithOne(d => d.PluginUsage)
            .HasForeignKey(d => d.PluginUsageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(u => new { u.EventId, u.PluginId }).IsUnique();
    }
}
