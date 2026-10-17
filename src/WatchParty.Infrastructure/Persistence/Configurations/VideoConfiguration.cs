using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Users;
using WatchParty.Domain.Videos;

namespace WatchParty.Infrastructure.Persistence.Configurations;

internal sealed class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Title).HasMaxLength(Video.TitleMaxLength).IsRequired();
        builder.Property(v => v.Description).HasMaxLength(Video.DescriptionMaxLength);
        builder.Property(v => v.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(v => v.FileKey).HasMaxLength(100).IsRequired();
        builder.Property(v => v.ThumbnailKey).HasMaxLength(100);
        builder.Property(v => v.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(v => v.FailureReason).HasMaxLength(500);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(v => v.UploadedBy).WithMany().HasForeignKey(v => v.UploadedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => v.UploadedAt);
    }
}

internal sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.HasKey(f => new { f.UserId, f.VideoId });
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Video>().WithMany().HasForeignKey(f => f.VideoId).OnDelete(DeleteBehavior.Cascade);
    }
}
