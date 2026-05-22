using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarcraftArchive.Api.Domain.Entities.Warcraft;

namespace WarcraftArchive.Api.Infrastructure.Persistence.Configurations;

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> e)
    {
        e.HasKey(co => co.Id);
        e.HasIndex(co => co.Name);
        e.HasIndex(co => co.Expansion);
        e.HasIndex(co => co.OwnerUserId);
        e.Property(co => co.Name).IsRequired().HasMaxLength(300);
        e.Property(co => co.Expansion).IsRequired().HasMaxLength(100);
        e.Property(co => co.Comment).HasMaxLength(1000);
        e.HasMany(co => co.Motives).WithMany(m => m.Contents).UsingEntity("ContentUserMotives");
        e.HasOne(co => co.OwnerUser).WithMany().HasForeignKey(co => co.OwnerUserId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
    }
}
