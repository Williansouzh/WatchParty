using FluentValidation;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Playlists;

namespace WatchParty.Application.Features.Playlists;

public sealed record CreatePlaylistCommand(Guid UserId, string Name, string? Description) : ICommand<Result<Guid>>;

internal sealed class CreatePlaylistCommandValidator : AbstractValidator<CreatePlaylistCommand>
{
    public CreatePlaylistCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Dê um nome à playlist.").MaximumLength(Playlist.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(Playlist.DescriptionMaxLength);
    }
}

internal sealed class CreatePlaylistCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<CreatePlaylistCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePlaylistCommand command, CancellationToken cancellationToken)
    {
        var playlist = Playlist.Create(command.Name, command.Description, command.UserId, time.GetUtcNow().UtcDateTime);
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(cancellationToken);
        return playlist.Id;
    }
}
