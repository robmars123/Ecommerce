namespace Ecommerce.Api.IntegrationTests.Catalog;

// Removing a cart line is a DELETE from the browser: its preflight must allow DELETE on /api/v1/cart and nowhere else public.
public class CartCorsTests
{
    private const string Allowed = "https://store.test.invalid";

    private static async Task<string> AllowedMethodsAsync(string path, string method)
    {
        await using ApiFactory factory = new(ApiFactory.Unreachable, settings: new Dictionary<string, string> { ["Cors:AllowedOrigins:0"] = Allowed });
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Options, path);
        request.Headers.Add("Origin", Allowed);
        request.Headers.Add("Access-Control-Request-Method", method);
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.Headers.TryGetValues("Access-Control-Allow-Methods", out IEnumerable<string>? values) ? string.Join(",", values) : "";
    }

    [Fact]
    public async Task A_cart_line_delete_preflight_is_allowed()
    {
        Assert.Contains("DELETE", await AllowedMethodsAsync("/api/v1/cart/items/00000000-0000-0000-0000-000000000001", "DELETE"));
    }

    [Fact]
    public async Task Delete_is_still_refused_on_the_catalog()
    {
        Assert.DoesNotContain("DELETE", await AllowedMethodsAsync("/api/v1/products", "DELETE"));
    }
}
