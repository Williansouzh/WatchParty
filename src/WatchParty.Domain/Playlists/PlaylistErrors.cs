using WatchParty.Domain.Common;

namespace WatchParty.Domain.Playlists;

public static class PlaylistErrors
{
    public static readonly Error NotFound = Error.NotFound("Playlist.NotFound", "Playlist não encontrada.");

    public static readonly Error NotOwner =
        Error.Forbidden("Playlist.NotOwner", "Só o dono pode alterar essa playlist.");

    public static readonly Error AlreadyContainsVideo =
        Error.Conflict("Playlist.AlreadyContainsVideo", "Esse vídeo já está na playlist.");

    public static readonly Error VideoNotInPlaylist =
        Error.NotFound("Playlist.VideoNotInPlaylist", "Esse vídeo não está na playlist.");
}
