using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Playlists;

namespace WatchParty.Infrastructure.Persistence.Configurations;

internal sealed class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(Playlist.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Playlist.DescriptionMaxLength);

        builder.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PlaylistItemConfiguration : IEntityTypeConfiguration<PlaylistItem>
{
    public void Configure(EntityTypeBuilder<PlaylistItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasOne(i => i.Video).WithMany().HasForeignKey(i => i.VideoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => new { i.PlaylistId, i.VideoId }).IsUnique();
    }
}
