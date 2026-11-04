using Ecommerce.SharedKernel.Time;
using Ecommerce.Api.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests;

public class StartupTests
{
    [Fact]
    public void Startup_fails_fast_when_connection_string_is_missing()
    {
        using var factory = new ApiFactory("");

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.Contains("ConnectionStrings:Postgres is not configured", AllMessages(ex));
    }

    [Fact]
    public void Container_resolves_IClock_as_SystemClock()
    {
        using var factory = new ApiFactory(ApiFactory.Unreachable);

        Assert.IsType<SystemClock>(factory.Services.GetRequiredService<IClock>());
    }

    private static string AllMessages(Exception? ex)
    {
        var parts = new List<string>();
        for (; ex is not null; ex = ex.InnerException)
        {
            parts.Add(ex.Message);
        }
        return string.Join(" | ", parts);
    }
}
