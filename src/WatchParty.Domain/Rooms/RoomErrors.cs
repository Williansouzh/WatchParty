using WatchParty.Domain.Common;

namespace WatchParty.Domain.Rooms;

public static class RoomErrors
{
    public static readonly Error NotFound = Error.NotFound("Room.NotFound", "Sala não encontrada.");

    public static readonly Error NotHost =
        Error.Forbidden("Room.NotHost", "Só quem criou a sala pode fazer isso.");

    public static readonly Error HostOnly =
        Error.Forbidden("Room.HostOnly", "Nesta sala só o anfitrião controla o vídeo.");

    public static readonly Error QueueItemNotFound =
        Error.NotFound("Room.QueueItemNotFound", "Esse item não está mais na fila.");

    public static readonly Error QueueEmpty =
        Error.Conflict("Room.QueueEmpty", "A fila está vazia.");

    public static readonly Error NoVideo =
        Error.Conflict("Room.NoVideo", "Escolha um vídeo para a sala primeiro.");
}
