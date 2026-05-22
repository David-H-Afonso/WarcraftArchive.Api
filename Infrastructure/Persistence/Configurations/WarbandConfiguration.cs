using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarcraftArchive.Api.Domain.Entities.Auth;

namespace WarcraftArchive.Api.Infrastructure.Persistence.Configurations;

public class WarbandConfiguration : IEntityTypeConfiguration<Warband>
{
    public void Configure(EntityTypeBuilder<Warband> e)
    {
        e.HasKey(w => w.Id);
        e.HasIndex(w => new { w.OwnerUserId, w.Name }).IsUnique();
        e.Property(w => w.Name).IsRequired().HasMaxLength(200);
        e.Property(w => w.Color).HasMaxLength(50);
        e.HasOne(w => w.OwnerUser).WithMany(u => u.Warbands).HasForeignKey(w => w.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
    }
}
