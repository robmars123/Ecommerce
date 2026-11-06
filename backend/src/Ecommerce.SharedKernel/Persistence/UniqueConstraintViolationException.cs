namespace Ecommerce.SharedKernel.Persistence;

/// <summary>A unique index rejected a write. <see cref="ConstraintName"/> is the index name.</summary>
public sealed class UniqueConstraintViolationException(string constraintName, Exception? inner = null)
    : Exception($"Unique constraint '{constraintName}' was violated.", inner)
{
    public string ConstraintName { get; } = constraintName;
}
