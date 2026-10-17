using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;

namespace WatchParty.Application.Behaviors;

internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var start = Stopwatch.GetTimestamp();

        var response = await next();

        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (response is Result { IsFailure: true } failure)
            logger.LogInformation("{Request} failed in {Elapsed:0}ms: {Error}", name, elapsed, failure.Error.Code);
        else
            logger.LogDebug("{Request} handled in {Elapsed:0}ms", name, elapsed);

        return response;
    }
}
