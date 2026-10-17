using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Users;

namespace WatchParty.Application.Features.Auth;

public sealed record RegisterUserCommand(string UserName, string DisplayName, string Password, string InviteCode)
    : ICommand<Result<UserDto>>;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.UserName)
            .NotEmpty().WithMessage("Informe um nome de usuário.")
            .Length(User.UserNameMinLength, User.UserNameMaxLength)
            .WithMessage($"O usuário deve ter entre {User.UserNameMinLength} e {User.UserNameMaxLength} caracteres.")
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Use apenas letras, números, '.', '-' ou '_' no usuário.");
        RuleFor(c => c.DisplayName)
            .NotEmpty().WithMessage("Informe seu nome.")
            .MaximumLength(User.DisplayNameMaxLength);
        RuleFor(c => c.Password)
            .MinimumLength(6).WithMessage("A senha deve ter pelo menos 6 caracteres.")
            .MaximumLength(128);
        RuleFor(c => c.InviteCode).NotEmpty().WithMessage("Informe o código de convite.");
    }
}

internal sealed class RegisterUserCommandHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<AuthOptions> options,
    TimeProvider time) : ICommandHandler<RegisterUserCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var expectedCode = options.Value.InviteCode;
        if (string.IsNullOrEmpty(expectedCode) || !string.Equals(command.InviteCode.Trim(), expectedCode, StringComparison.Ordinal))
            return UserErrors.InvalidInviteCode;

        var userName = User.NormalizeUserName(command.UserName);
        if (await db.Users.AnyAsync(u => u.UserName == userName, cancellationToken))
            return UserErrors.UserNameTaken;

        var user = User.Create(userName, command.DisplayName, passwordHasher.Hash(command.Password), time.GetUtcNow().UtcDateTime);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return UserDto.From(user);
    }
}
