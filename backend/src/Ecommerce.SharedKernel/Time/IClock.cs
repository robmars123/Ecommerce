namespace Ecommerce.SharedKernel.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
