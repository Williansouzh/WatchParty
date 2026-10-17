using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using WatchParty.Domain.Common;

namespace WatchParty.Api.Hubs;

/// <summary>Turns validation/domain errors into messages the browser can show.</summary>
internal sealed class HubExceptionFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (ValidationException ex)
        {
            throw new HubException(ex.Errors.FirstOrDefault()?.ErrorMessage ?? "Dados inválidos.");
        }
        catch (DomainException ex)
        {
            throw new HubException(ex.Message);
        }
    }
}
