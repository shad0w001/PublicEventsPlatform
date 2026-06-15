using Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Plugins;

public class PluginDataConfiguration : IEntityTypeConfiguration<PluginData>
{
    public void Configure(EntityTypeBuilder<PluginData> builder)
    {
        builder.ToTable("plugin_data");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Key)
            .IsRequired()
            .HasMaxLength(PluginConstants.DataKeyMaxLength);

        builder.HasIndex(d => new { d.PluginUsageId, d.Key })
            .IsUnique();
    }
}
