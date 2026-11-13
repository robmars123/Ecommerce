using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ecommerce.Api.IntegrationTests.Catalog.Admin;
using Ecommerce.Api.IntegrationTests.Identity;

namespace Ecommerce.Api.IntegrationTests.Catalog;

// IF1 .. IF3: the public image route over HTTP with a real Postgres. 401/403/400 do not apply: the route is public and read-only
// (an invalid or unknown key is the same 404).
public class ImageFileApiTests(PostgresFixture postgres)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private static async Task<(string ProductId, string Key, string Url)> UploadAsync(IdentityTestEnv env, string token)
    {
        string id = (await AdminSamples.CreateProductAsync(env, token)).Str("id");
        using MultipartFormDataContent form = new MultipartFormDataContent();
        ByteArrayContent file = new ByteArrayContent(Png);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        form.Add(file, "file", "a.png");
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{AdminSamples.Products}/{id}/images") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await env.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        return (id, json.GetProperty("storageKey").GetString()!, json.GetProperty("url").GetString()!);
    }

    [Fact]
    public async Task IF1_an_anonymous_get_returns_the_bytes_with_immutable_caching_and_nosniff_and_bad_or_unknown_keys_are_404()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        string token = await AdminSamples.AdminTokenAsync(env);
        (string id, string _, string url) = await UploadAsync(env, token);

        using HttpResponseMessage ok = await env.Client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("image/png", ok.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await ok.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        string cache = ok.Headers.CacheControl?.ToString() ?? "";
        Assert.Contains("max-age=31536000", cache);
        Assert.Contains("immutable", cache);
        Assert.NotNull(ok.Headers.ETag);
        Assert.Equal("nosniff", Assert.Single(ok.Headers.GetValues("X-Content-Type-Options")));

        string[] notFound =
        [
            $"/api/v1/images/products/{id}/{Guid.NewGuid():D}.png",
            "/api/v1/images/products/../x.png",
            "/api/v1/images/products//x.png",
            "/api/v1/images/products%5Cx.png",
        ];
        foreach (string path in notFound)
        {
            using HttpResponseMessage response = await env.Client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{path} gave {response.StatusCode}");
        }
    }

    [Fact]
    public async Task IF2_a_matching_if_none_match_is_304_with_an_empty_body()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        string token = await AdminSamples.AdminTokenAsync(env);
        (string _, string _, string url) = await UploadAsync(env, token);
        using HttpResponseMessage first = await env.Client.GetAsync(url, TestContext.Current.CancellationToken);

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using HttpResponseMessage second = await env.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IF3_a_published_products_storefront_image_url_is_the_upload_url_and_it_serves_the_image()
    {
        await using IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        string token = await AdminSamples.AdminTokenAsync(env);
        (string id, string key, string url) = await UploadAsync(env, token);
        string path = $"{AdminSamples.Products}/{id}";
        IdentityTestEnv.Api created = await env.GetAsync(path, token);
        IdentityTestEnv.Api withImage = await env.SendAsync(HttpMethod.Put, path,
            AdminSamples.UpdateBody(created.Json, variantActive: true, images: [new { storageKey = key, altText = "Front" }]), token);
        Assert.Equal(HttpStatusCode.OK, withImage.Status);
        Assert.Equal(HttpStatusCode.OK, (await env.PostAsync(path + "/publish", null, token)).Status);

        IdentityTestEnv.Api detail = await env.GetAsync("/api/v1/products/" + created.Str("slug"));

        Assert.Equal(url, detail.Json.GetProperty("images")[0].GetProperty("url").GetString());
        using HttpResponseMessage image = await env.Client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
    }
}
