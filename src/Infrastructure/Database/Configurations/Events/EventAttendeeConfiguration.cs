using Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations.Events;

public class EventAttendeeConfiguration : IEntityTypeConfiguration<EventAttendee>
{
    public void Configure(EntityTypeBuilder<EventAttendee> builder)
    {
        builder.ToTable("event_attendees");

        builder.HasKey(ea => new { ea.EventId, ea.ParticipantId });

        builder.HasOne(ea => ea.Event)
            .WithMany(e => e.Attendees)
            .HasForeignKey(ea => ea.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ea => ea.Participant)
            .WithMany(p => p.AttendedEvents)
            .HasForeignKey(ea => ea.ParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(ea => ea.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(ea => ea.TicketCount);
    }
}
