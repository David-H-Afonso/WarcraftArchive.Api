using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarcraftArchive.Api.Domain.Entities.Warcraft;

namespace WarcraftArchive.Api.Infrastructure.Persistence.Configurations;

public class TrackingConfiguration : IEntityTypeConfiguration<Tracking>
{
    public void Configure(EntityTypeBuilder<Tracking> e)
    {
        e.HasKey(t => t.Id);
        e.HasIndex(t => new { t.CharacterId, t.ContentId, t.Difficulty }).IsUnique();
        e.HasIndex(t => t.Status);
        e.HasIndex(t => t.Frequency);
        e.Property(t => t.Comment).HasMaxLength(1000);
        e.HasOne(t => t.Character).WithMany(c => c.Trackings).HasForeignKey(t => t.CharacterId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(t => t.Content).WithMany(co => co.Trackings).HasForeignKey(t => t.ContentId).OnDelete(DeleteBehavior.Cascade);
    }
}
