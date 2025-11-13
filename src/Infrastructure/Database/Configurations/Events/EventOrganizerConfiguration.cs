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

            builder.HasKey(eo => new { eo.EventId, eo.ParticipantId });

            builder.HasOne(eo => eo.Event)
                .WithMany(e => e.Organizers)
                .HasForeignKey(eo => eo.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(eo => eo.Participant)
                .WithMany(p => p.OrganizedEvents)
                .HasForeignKey(eo => eo.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
