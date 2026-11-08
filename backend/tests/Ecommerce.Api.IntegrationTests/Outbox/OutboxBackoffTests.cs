using Ecommerce.Persistence.Outbox;

namespace Ecommerce.Api.IntegrationTests.Outbox;

// Retry spacing (arch 8.4): 2^attempts seconds, capped at five minutes. Pure arithmetic, no database.
public class OutboxBackoffTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(5, 32)]
    [InlineData(8, 256)]
    [InlineData(9, 300)]
    [InlineData(40, 300)]
    public void The_delay_doubles_per_attempt_and_stops_at_five_minutes(int attempts, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), OutboxBackoff.DelayAfter(attempts));

    [Fact]
    public void More_than_five_attempts_is_a_dead_letter() => Assert.Equal(5, OutboxBackoff.MaxAttempts);
}
