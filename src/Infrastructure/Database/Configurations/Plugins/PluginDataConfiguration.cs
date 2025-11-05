using Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Database.Configurations.Plugins
{
    public class PluginDataConfiguration : IEntityTypeConfiguration<PluginData>
    {
        public void Configure(EntityTypeBuilder<PluginData> builder)
        {
            builder.ToTable("plugin_data");
        }
    }
}
