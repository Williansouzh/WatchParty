using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Playlists;

namespace WatchParty.Application.Features.Playlists;

public sealed record UpdatePlaylistCommand(Guid PlaylistId, Guid UserId, string Name, string? Description) : ICommand<Result>;

internal sealed class UpdatePlaylistCommandValidator : AbstractValidator<UpdatePlaylistCommand>
{
    public UpdatePlaylistCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Dê um nome à playlist.").MaximumLength(Playlist.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(Playlist.DescriptionMaxLength);
    }
}

internal sealed class UpdatePlaylistCommandHandler(IAppDbContext db) : ICommandHandler<UpdatePlaylistCommand, Result>
{
    public async Task<Result> Handle(UpdatePlaylistCommand command, CancellationToken cancellationToken)
    {
        var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.Id == command.PlaylistId, cancellationToken);
        if (playlist is null) return PlaylistErrors.NotFound;
        if (!playlist.IsOwnedBy(command.UserId)) return PlaylistErrors.NotOwner;

        playlist.Rename(command.Name, command.Description);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
