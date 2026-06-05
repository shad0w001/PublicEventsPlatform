using Domain.Events;
using Domain.Events.EventLocations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Database.Configurations.Events
{
    public class EventConfiguration : IEntityTypeConfiguration<Event>
    {
        public void Configure(EntityTypeBuilder<Event> builder)
        {
            builder.ToTable("events");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Description)
                .IsRequired();

            builder.Property(e => e.BannerImageUrl)
                .HasMaxLength(500);

            builder.Property(e => e.StartTime)
                .IsRequired();

            builder.Property(e => e.EndTime)
                .IsRequired();

            builder.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(e => e.LocationType)
                .IsRequired()
                .HasConversion<string>();

            //owned collection of EventLocations
            builder.OwnsMany(e => e.Locations, l =>
            {
                l.ToTable("event_locations");

                l.WithOwner().HasForeignKey("EventId");
                l.Property<Guid>("Id");
                l.HasKey("Id");

                l.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                l.Property(x => x.Date)
                    .IsRequired();

                l.Property(x => x.Kind)
                    .IsRequired()
                    .HasConversion<string>();

                l.Property(x => x.Url).HasMaxLength(500);
                l.Property(x => x.Address).HasMaxLength(500);
                l.Property(x => x.City).HasMaxLength(200);
                l.Property(x => x.Country).HasMaxLength(200);
                l.Property(x => x.Latitude);
                l.Property(x => x.Longitude);
                l.Property(x => x.ExternalPlaceId).HasMaxLength(200);
            });
        }
    }
}
