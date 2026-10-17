namespace WatchParty.Application.Abstractions.Messaging;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>Cross-cutting step wrapped around every handler (validation, logging...).</summary>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
