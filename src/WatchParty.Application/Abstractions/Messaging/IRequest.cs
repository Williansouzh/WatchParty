namespace WatchParty.Application.Abstractions.Messaging;

/// <summary>Marker for anything that can be dispatched through <see cref="ISender"/>.</summary>
public interface IRequest<TResponse>;

/// <summary>A request that changes state.</summary>
public interface ICommand<TResponse> : IRequest<TResponse>;

/// <summary>A request that only reads state.</summary>
public interface IQuery<TResponse> : IRequest<TResponse>;
