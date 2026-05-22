using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarcraftArchive.Api.Domain.Entities.Auth;

namespace WarcraftArchive.Api.Infrastructure.Persistence.Configurations;

public class UserMotiveConfiguration : IEntityTypeConfiguration<UserMotive>
{
    public void Configure(EntityTypeBuilder<UserMotive> e)
    {
        e.HasKey(m => m.Id);
        e.HasIndex(m => new { m.OwnerUserId, m.Name }).IsUnique();
        e.Property(m => m.Name).IsRequired().HasMaxLength(200);
        e.Property(m => m.Color).HasMaxLength(50);
        e.HasOne(m => m.OwnerUser).WithMany(u => u.UserMotives).HasForeignKey(m => m.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
    }
}
