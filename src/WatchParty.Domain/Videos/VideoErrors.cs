using WatchParty.Domain.Common;

namespace WatchParty.Domain.Videos;

public static class VideoErrors
{
    public static readonly Error NotFound = Error.NotFound("Video.NotFound", "Vídeo não encontrado.");

    public static readonly Error NotOwner =
        Error.Forbidden("Video.NotOwner", "Só quem enviou o vídeo pode alterá-lo.");

    public static readonly Error NotReady =
        Error.Conflict("Video.NotReady", "O vídeo ainda está sendo processado.");

    public static readonly Error UnsupportedFormat =
        Error.Validation("Video.UnsupportedFormat", "Formato de vídeo não suportado.");

    public static readonly Error NoThumbnail =
        Error.NotFound("Video.NoThumbnail", "Esse vídeo não tem miniatura.");
}
