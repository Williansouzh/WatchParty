namespace WatchParty.Domain.Common;

/// <summary>Thrown when an entity invariant is violated.</summary>
public sealed class DomainException(string message) : Exception(message);
