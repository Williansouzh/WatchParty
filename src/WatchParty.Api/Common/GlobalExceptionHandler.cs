using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using WatchParty.Domain.Common;

namespace WatchParty.Api.Common;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var result = exception switch
        {
            ValidationException validation => Results.ValidationProblem(
                validation.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                title: validation.Errors.FirstOrDefault()?.ErrorMessage ?? "Dados inválidos."),
            DomainException domain => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: domain.Message),
            BadHttpRequestException badRequest => Results.Problem(statusCode: badRequest.StatusCode, title: "Requisição inválida."),
            _ => null
        };

        if (result is null)
        {
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested) return true;

            logger.LogError(exception, "Unhandled exception");
            result = Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Algo deu errado. Tente novamente.");
        }

        await result.ExecuteAsync(httpContext);
        return true;
    }
}
