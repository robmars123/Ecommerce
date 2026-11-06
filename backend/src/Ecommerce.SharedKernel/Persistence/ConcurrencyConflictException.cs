namespace Ecommerce.SharedKernel.Persistence;

/// <summary>An optimistic-concurrency check failed: the row changed since it was read.</summary>
public sealed class ConcurrencyConflictException(string message, Exception? inner = null) : Exception(message, inner);
