using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Users;

namespace WatchParty.Application.Features.Auth;

public sealed record LoginCommand(string UserName, string Password) : ICommand<Result<UserDto>>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.UserName).NotEmpty().WithMessage("Informe o usuário.");
        RuleFor(c => c.Password).NotEmpty().WithMessage("Informe a senha.");
    }
}

internal sealed class LoginCommandHandler(IAppDbContext db, IPasswordHasher passwordHasher)
    : ICommandHandler<LoginCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var userName = User.NormalizeUserName(command.UserName);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

        if (user is null || !passwordHasher.Verify(user.PasswordHash, command.Password))
            return UserErrors.InvalidCredentials;

        return UserDto.From(user);
    }
}
