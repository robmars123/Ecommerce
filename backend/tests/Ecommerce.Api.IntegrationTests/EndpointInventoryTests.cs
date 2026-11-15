using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Api.IntegrationTests;

// Pins every route with its methods, auth policies, anonymous flag and CORS policy. Hand-checked against the controller attributes.
public class EndpointInventoryTests
{
    private static readonly string[] Expected =
    [
        "* health/live | - | anon | -",
        "* health/ready | - | anon | -",
        "* hubs/orders | (default) | auth | -",
        "* hubs/orders/negotiate | (default) | auth | -",
        "* {*path:nonfile} | - | anon | -",
        "DELETE api/v1/admin/categories/{slug} | Admin | auth | credentialed",
        "DELETE api/v1/cart/items/{variantId:guid} | (default) | auth | cart",
        "GET api/v1/admin/customers | AdminOrSupport | auth | credentialed",
        "GET api/v1/admin/customers/{id:guid} | AdminOrSupport | auth | credentialed",
        "GET api/v1/admin/dashboard | AdminOrSupport | auth | credentialed",
        "GET api/v1/admin/dashboard/export | Admin | auth | credentialed",
        "GET api/v1/admin/dashboard/recent-orders | AdminOrSupport | auth | credentialed",
        "GET api/v1/admin/dashboard/trend | Admin | auth | credentialed",
        "GET api/v1/admin/inventory | Admin | auth | credentialed",
        "GET api/v1/admin/orders | AdminOrSupport | auth | credentialed",
        "GET api/v1/admin/orders/{id:guid} | AdminOrSupport | auth | credentialed",
        "GET api/v1/admin/products | Admin | auth | credentialed",
        "GET api/v1/admin/products/{id:guid} | Admin | auth | credentialed",
        "GET api/v1/admin/products/{id:guid}/preview | Admin | auth | credentialed",
        "GET api/v1/admin/returns | Admin | auth | credentialed",
        "GET api/v1/admin/search | AdminOrSupport | auth | credentialed",
        "GET api/v1/admin/settings | Admin | auth | credentialed",
        "GET api/v1/auth/me | (default) | auth | credentialed",
        "GET api/v1/cart | (default) | auth | cart",
        "GET api/v1/categories | - | anon | -",
        "GET api/v1/images/{**key} | - | anon | -",
        "GET api/v1/orders | (default) | auth | -",
        "GET api/v1/orders/{id:guid} | (default) | auth | -",
        "GET api/v1/products | - | anon | -",
        "GET api/v1/products/{slug} | - | anon | -",
        "GET api/v1/shipping-methods | (default) | auth | -",
        "POST api/v1/admin/categories | Admin | auth | credentialed",
        "POST api/v1/admin/inventory/adjust | Admin | auth | credentialed",
        "POST api/v1/admin/orders/{id:guid}/deliver | Admin | auth | credentialed",
        "POST api/v1/admin/orders/{id:guid}/process | Admin | auth | credentialed",
        "POST api/v1/admin/orders/{id:guid}/refund | Admin | auth | credentialed",
        "POST api/v1/admin/orders/{id:guid}/ship | Admin | auth | credentialed",
        "POST api/v1/admin/orders/{id:guid}/status-override | Admin | auth | credentialed",
        "POST api/v1/admin/orders/{id:guid}/unfulfillable | Admin | auth | credentialed",
        "POST api/v1/admin/products | Admin | auth | credentialed",
        "POST api/v1/admin/products/{id:guid}/images | Admin | auth | credentialed",
        "POST api/v1/admin/products/{id:guid}/publish | Admin | auth | credentialed",
        "POST api/v1/admin/products/{id:guid}/unpublish | Admin | auth | credentialed",
        "POST api/v1/admin/projections/{name}/replay | Admin | auth | credentialed",
        "POST api/v1/admin/returns/{id:guid}/approve | Admin | auth | credentialed",
        "POST api/v1/admin/returns/{id:guid}/decline | Admin | auth | credentialed",
        "POST api/v1/auth/login | - | anon | credentialed",
        "POST api/v1/auth/logout | - | anon | credentialed",
        "POST api/v1/auth/password-reset | - | anon | credentialed",
        "POST api/v1/auth/password-reset/confirm | - | anon | credentialed",
        "POST api/v1/auth/refresh | - | anon | credentialed",
        "POST api/v1/auth/register | - | anon | credentialed",
        "POST api/v1/cart/items | (default) | auth | cart",
        "POST api/v1/cart/merge | (default) | auth | cart",
        "POST api/v1/checkout | (default) | auth | -",
        "POST api/v1/orders/{id:guid}/retry-payment | (default) | auth | -",
        "POST api/v1/orders/{id:guid}/return-request | (default) | auth | -",
        "POST api/v1/webhooks/stripe | - | anon | -",
        "PUT api/v1/admin/categories/{slug} | Admin | auth | credentialed",
        "PUT api/v1/admin/products/{id:guid} | Admin | auth | credentialed",
        "PUT api/v1/cart/items/{variantId:guid} | (default) | auth | cart",
    ];

    [Fact]
    public void T1_every_endpoint_has_the_expected_methods_auth_and_cors()
    {
        using ApiFactory factory = new ApiFactory(ApiFactory.Unreachable);

        List<string> actual = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(Describe)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(string.Join("\n", Expected), string.Join("\n", actual));
    }

    private static string Describe(RouteEndpoint endpoint)
    {
        IReadOnlyList<string>? httpMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        string methods = httpMethods is { Count: > 0 } ? string.Join(",", httpMethods) : "*";

        IReadOnlyList<string> policyList = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy ?? "(default)").ToList();
        string policies = policyList.Count > 0 ? string.Join("+", policyList) : "-";

        string access = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ? "anon" : "auth";
        string cors = endpoint.Metadata.GetMetadata<IEnableCorsAttribute>()?.PolicyName ?? "-";

        return $"{methods} {endpoint.RoutePattern.RawText!.TrimStart('/')} | {policies} | {access} | {cors}";
    }
}
