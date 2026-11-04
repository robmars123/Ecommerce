using Ecommerce.SharedKernel.Time;

namespace Ecommerce.Api.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => TimeProvider.System.GetUtcNow();
}
