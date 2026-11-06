using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Domain;

internal sealed record LockoutPolicy
{
    public LockoutPolicy(int maxFailedAttempts, TimeSpan duration)
    {
        if (maxFailedAttempts < 1)
        {
            throw new DomainException("Lockout needs at least 1 allowed attempt.");
        }
        if (duration <= TimeSpan.Zero)
        {
            throw new DomainException("Lockout duration must be positive.");
        }
        MaxFailedAttempts = maxFailedAttempts;
        Duration = duration;
    }

    public int MaxFailedAttempts { get; }
    public TimeSpan Duration { get; }
}
