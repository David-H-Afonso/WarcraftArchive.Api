using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WarcraftArchive.Api.Domain.Entities.Auth;

namespace WarcraftArchive.Api.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.HasKey(u => u.Id);
        e.HasIndex(u => u.Email).IsUnique();
        e.Property(u => u.Email).IsRequired().HasMaxLength(320);
        e.Property(u => u.UserName).IsRequired().HasMaxLength(100);
        e.Property(u => u.PasswordHash).IsRequired();
    }
}
