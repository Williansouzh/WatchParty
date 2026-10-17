using FluentValidation;
using WatchParty.Application.Features.Auth;
using WatchParty.Domain.Users;

namespace WatchParty.Tests.Features;

public class AuthTests
{
    [Fact]
    public async Task Register_requires_the_invite_code()
    {
        await using var app = new TestApp();

        var result = await app.Send(new RegisterUserCommand("will", "Willian", "senha123", "errado"));

        Assert.Equal(UserErrors.InvalidInviteCode, result.Error);
    }

    [Fact]
    public async Task Registered_user_can_log_in_with_any_casing_of_the_user_name()
    {
        await using var app = new TestApp();
        await app.Send(new RegisterUserCommand("Will", "Willian", "senha123", TestApp.InviteCode));

        var ok = await app.Send(new LoginCommand("WILL", "senha123"));
        var wrong = await app.Send(new LoginCommand("will", "outra-senha"));

        Assert.True(ok.IsSuccess);
        Assert.Equal("will", ok.Value.UserName);
        Assert.Equal(UserErrors.InvalidCredentials, wrong.Error);
    }

    [Fact]
    public async Task User_names_are_unique()
    {
        await using var app = new TestApp();
        await app.Send(new RegisterUserCommand("will", "Willian", "senha123", TestApp.InviteCode));

        var result = await app.Send(new RegisterUserCommand("WILL", "Outro", "senha123", TestApp.InviteCode));

        Assert.Equal(UserErrors.UserNameTaken, result.Error);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_by_the_validation_pipeline()
    {
        await using var app = new TestApp();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            app.Send(new RegisterUserCommand("a", "", "123", TestApp.InviteCode)));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(RegisterUserCommand.Password));
    }
}
