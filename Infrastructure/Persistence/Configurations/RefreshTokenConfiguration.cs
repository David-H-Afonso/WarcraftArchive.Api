using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarcraftArchive.Api.Domain.Entities.Auth;

namespace WarcraftArchive.Api.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.HasKey(rt => rt.Id);
        e.HasIndex(rt => rt.TokenHash).IsUnique();
        e.HasIndex(rt => rt.UserId);
        e.Property(rt => rt.TokenHash).IsRequired();
        e.HasOne(rt => rt.User).WithMany(u => u.RefreshTokens).HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
