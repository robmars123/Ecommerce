namespace Ecommerce.SharedKernel.Persistence;

/// <summary>A foreign key rejected a write or delete. <see cref="ConstraintName"/> is the constraint name.</summary>
public sealed class ForeignKeyViolationException(string constraintName, Exception? inner = null)
    : Exception($"Foreign key '{constraintName}' was violated.", inner)
{
    public string ConstraintName { get; } = constraintName;
}
