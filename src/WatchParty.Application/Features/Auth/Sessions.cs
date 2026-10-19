using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Users;

namespace WatchParty.Application.Features.Auth;

/// <summary>Changes the password and ends every other login (the stamp rotates). Returns the new session.</summary>
public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : ICommand<Result<AuthSession>>;

/// <summary>Invalidates every cookie and token of the user ("sair de todos os aparelhos").</summary>
public sealed record SignOutEverywhereCommand(Guid UserId) : ICommand<Result>;

/// <summary>Current stamp of a user, or null if the user no longer exists.</summary>
public sealed record GetSecurityStampQuery(Guid UserId) : IQuery<string?>;

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(c => c.NewPassword)
            .MinimumLength(6).WithMessage("A nova senha deve ter pelo menos 6 caracteres.")
            .MaximumLength(128);
    }
}

internal sealed class ChangePasswordCommandHandler(IAppDbContext db, IPasswordHasher hasher)
    : ICommandHandler<ChangePasswordCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null) return UserErrors.NotFound;
        if (!hasher.Verify(user.PasswordHash, command.CurrentPassword)) return UserErrors.WrongPassword;

        user.ChangePassword(hasher.Hash(command.NewPassword));
        await db.SaveChangesAsync(cancellationToken);
        return AuthSession.From(user);
    }
}

internal sealed class SignOutEverywhereCommandHandler(IAppDbContext db) : ICommandHandler<SignOutEverywhereCommand, Result>
{
    public async Task<Result> Handle(SignOutEverywhereCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null) return UserErrors.NotFound;

        user.RotateSecurityStamp();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class GetSecurityStampQueryHandler(IAppDbContext db) : IQueryHandler<GetSecurityStampQuery, string?>
{
    public Task<string?> Handle(GetSecurityStampQuery query, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().Where(u => u.Id == query.UserId).Select(u => u.SecurityStamp).FirstOrDefaultAsync(cancellationToken);
}
