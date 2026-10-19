using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WatchParty.Domain.Rooms;

namespace WatchParty.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Code).HasMaxLength(RoomCode.Length).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(Room.NameMaxLength).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.HasIndex(r => r.LastActivityAt);

        builder.HasOne(r => r.Host).WithMany().HasForeignKey(r => r.HostId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CurrentVideo).WithMany().HasForeignKey(r => r.CurrentVideoId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Content).HasMaxLength(ChatMessage.ContentMaxLength).IsRequired();

        builder.HasOne<Room>().WithMany().HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.RoomId, m.SentAt });
    }
}

internal sealed class RoomQueueItemConfiguration : IEntityTypeConfiguration<RoomQueueItem>
{
    public void Configure(EntityTypeBuilder<RoomQueueItem> builder)
    {
        builder.HasKey(q => q.Id);
        builder.HasOne<Room>().WithMany().HasForeignKey(q => q.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(q => q.Video).WithMany().HasForeignKey(q => q.VideoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(q => q.AddedBy).WithMany().HasForeignKey(q => q.AddedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(q => new { q.RoomId, q.Position });
    }
}
