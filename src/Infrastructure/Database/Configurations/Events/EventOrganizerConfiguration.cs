using Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Database.Configurations.Events
{
    public class EventOrganizerConfiguration : IEntityTypeConfiguration<EventOrganizer>
    {
        public void Configure(EntityTypeBuilder<EventOrganizer> builder)
        {
            builder.ToTable("event_organizers");

            builder.HasKey(o => new { o.EventId, o.ActorId });

            builder.Property(o => o.EventId).IsRequired();
            builder.Property(o => o.ActorId).IsRequired();
        }
    }
}
