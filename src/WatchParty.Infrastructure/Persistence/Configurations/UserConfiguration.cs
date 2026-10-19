using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Users;

namespace WatchParty.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.UserName).HasMaxLength(User.UserNameMaxLength).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.AvatarColor).HasMaxLength(9).IsRequired();
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired().HasDefaultValue("initial");
        builder.HasIndex(u => u.UserName).IsUnique();
    }
}
