using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarcraftArchive.Api.Domain.Entities.Warcraft;

namespace WarcraftArchive.Api.Infrastructure.Persistence.Configurations;

public class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> e)
    {
        e.HasKey(c => c.Id);
        e.HasIndex(c => c.Name);
        e.Property(c => c.Name).IsRequired().HasMaxLength(200);
        e.Property(c => c.Class).IsRequired().HasMaxLength(100);
        e.Property(c => c.Race).HasMaxLength(100);
        e.Property(c => c.Covenant).HasMaxLength(100);
        e.HasOne(c => c.OwnerUser).WithMany().HasForeignKey(c => c.OwnerUserId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        e.HasOne(c => c.Warband).WithMany(w => w.Characters).HasForeignKey(c => c.WarbandId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
    }
}
