using Domain.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Database.Configurations.Actors
{
    public class ActorConfiguration : IEntityTypeConfiguration<Participant>
    {
        public void Configure(EntityTypeBuilder<Participant> builder)
        {
            builder.ToTable("actors");

            builder.HasKey(a => a.Id);

            //this is an enum
            builder.Property(a => a.Type)
                .IsRequired()
                .HasConversion<string>();
        }
    }
}
