using WatchParty.Domain.Common;

namespace WatchParty.Domain.Users;

public static class UserErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("User.InvalidCredentials", "Usuário ou senha inválidos.");

    public static readonly Error InvalidInviteCode =
        Error.Forbidden("User.InvalidInviteCode", "Código de convite inválido.");

    public static readonly Error UserNameTaken =
        Error.Conflict("User.UserNameTaken", "Esse nome de usuário já está em uso.");

    public static readonly Error NotFound =
        Error.NotFound("User.NotFound", "Usuário não encontrado.");
}
