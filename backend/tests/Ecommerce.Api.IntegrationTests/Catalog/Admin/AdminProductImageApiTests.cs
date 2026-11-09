using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecommerce.Api.IntegrationTests.Identity;
using Ecommerce.Catalog.Application.Admin;
using Ecommerce.Identity.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ecommerce.Api.IntegrationTests.Catalog.Admin;

// IU1 .. IU9 (IU5: the host's anonymous 404 fallback hides the 415 a mismatched Consumes would give): product image upload over HTTP with a real Postgres and a recording storage fake (no network).
public class AdminProductImageApiTests(PostgresFixture postgres)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 74, 70, 73, 70, 0, 1, 1, 2, 3];
    private static readonly byte[] WebP = [.. "RIFF"u8, 20, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8, 1, 2, 3, 4];

    internal sealed class RecordingImageStorage : IImageStorage
    {
        public List<(string Key, string ContentType, byte[] Bytes)> Puts { get; } = [];
        public Exception? Throw { get; set; }

        public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
        {
            if (Throw is not null)
            {
                throw Throw;
            }
            using MemoryStream copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);
            Puts.Add((key, contentType, copy.ToArray()));
        }
    }

    private async Task<(IdentityTestEnv Env, RecordingImageStorage Storage, string Token)> StartAsync()
    {
        RecordingImageStorage storage = new RecordingImageStorage();
        IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres, configure: s =>
        {
            s.RemoveAll<IImageStorage>();
            s.AddSingleton<IImageStorage>(storage);
        });
        return (env, storage, await AdminSamples.AdminTokenAsync(env));
    }

    private static async Task<(HttpStatusCode Status, string Text)> UploadAsync(
        IdentityTestEnv env, string productId, byte[]? bytes, string? token, string contentType = "image/png", string fileName = "a.png", bool filePart = true)
    {
        using MultipartFormDataContent form = new MultipartFormDataContent();
        if (filePart && bytes is not null)
        {
            ByteArrayContent file = new ByteArrayContent(bytes);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            form.Add(file, "file", fileName);
        }
        else
        {
            form.Add(new StringContent("x"), "other");
        }
        return await SendAsync(env, productId, form, token);
    }

    private static async Task<(HttpStatusCode Status, string Text)> SendAsync(IdentityTestEnv env, string productId, HttpContent content, string? token)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{AdminSamples.Products}/{productId}/images") { Content = content };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        using HttpResponseMessage response = await env.Client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string KeyOf(string text) => JsonDocument.Parse(text).RootElement.GetProperty("storageKey").GetString()!;

    [Fact]
    public async Task IU1_a_png_is_stored_byte_identical_returns_its_key_and_attaches_nothing_until_the_product_is_saved()
    {
        (IdentityTestEnv? env, RecordingImageStorage? storage, string? token) = await StartAsync();
        await using IdentityTestEnv _env = env;
        IdentityTestEnv.Api product = await AdminSamples.CreateProductAsync(env, token);
        string id = product.Str("id");

        (HttpStatusCode status, string text) = await UploadAsync(env, id, Png, token);

        Assert.Equal(HttpStatusCode.OK, status);
        string key = KeyOf(text);
        Assert.Matches(new Regex($"^products/{id}/[0-9a-f-]{{36}}\\.png$"), key);
        (string Key, string ContentType, byte[] Bytes) put = Assert.Single(storage.Puts);
        Assert.Equal(key, put.Key);
        Assert.Equal("image/png", put.ContentType);
        Assert.Equal(Png, put.Bytes);

        IdentityTestEnv.Api before = await env.GetAsync($"{AdminSamples.Products}/{id}", token);
        Assert.Empty(before.Json.GetProperty("images").EnumerateArray());

        IdentityTestEnv.Api saved = await env.SendAsync(HttpMethod.Put, $"{AdminSamples.Products}/{id}",
            AdminSamples.UpdateBody(before.Json, images: [new { storageKey = key, altText = "Front" }]), token);
        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal(key, saved.Json.GetProperty("images")[0].GetProperty("storageKey").GetString());
    }

    [Fact]
    public async Task IU2_jpeg_and_webp_are_detected_from_the_bytes_and_the_clients_type_and_name_are_ignored()
    {
        (IdentityTestEnv? env, RecordingImageStorage? storage, string? token) = await StartAsync();
        await using IdentityTestEnv _env = env;
        string id = (await AdminSamples.CreateProductAsync(env, token)).Str("id");

        (HttpStatusCode s1, string t1) = await UploadAsync(env, id, Jpeg, token, "text/plain", "x.exe");
        (HttpStatusCode s2, string t2) = await UploadAsync(env, id, WebP, token, "text/plain", "x.exe");

        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.EndsWith(".jpg", KeyOf(t1));
        Assert.EndsWith(".webp", KeyOf(t2));
        Assert.Equal(["image/jpeg", "image/webp"], storage.Puts.Select(p => p.ContentType).ToArray());
    }

    [Fact]
    public async Task IU3_upload_needs_an_admin_token()
    {
        (IdentityTestEnv? env, RecordingImageStorage? storage, string? admin) = await StartAsync();
        await using IdentityTestEnv _env = env;
        string id = (await AdminSamples.CreateProductAsync(env, admin)).Str("id");
        await env.AddUserAsync("cust@example.test", UserRole.Customer);
        await env.AddUserAsync("support@example.test", UserRole.Support);

        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(env, id, Png, null)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(env, id, Png, await env.AccessTokenAsync("cust@example.test"))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(env, id, Png, await env.AccessTokenAsync("support@example.test"))).Status);
        Assert.Empty(storage.Puts);
    }

    public static TheoryData<string> BadFiles => new()
    {
        "none", "empty", "toobig", "svg", "html", "wave", "gif",
    };

    [Theory]
    [MemberData(nameof(BadFiles))]
    public async Task IU4_a_missing_empty_oversize_or_non_image_file_is_400_on_file_and_nothing_is_stored(string kind)
    {
        (IdentityTestEnv? env, RecordingImageStorage? storage, string? token) = await StartAsync();
        await using IdentityTestEnv _env = env;
        string id = (await AdminSamples.CreateProductAsync(env, token)).Str("id");

        (HttpStatusCode status, string text) = kind switch
        {
            "none" => await UploadAsync(env, id, null, token, filePart: false),
            "empty" => await UploadAsync(env, id, [], token),
            "toobig" => await UploadAsync(env, id, [.. Png, .. new byte[5 * 1024 * 1024 + 1 - Png.Length]], token),
            "svg" => await UploadAsync(env, id, Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"), token, "image/png", "a.png"),
            "html" => await UploadAsync(env, id, Encoding.UTF8.GetBytes("<html><script>1</script></html>"), token),
            "wave" => await UploadAsync(env, id, [.. "RIFF"u8, 20, 0, 0, 0, .. "WAVE"u8, 1, 2, 3, 4], token),
            _ => await UploadAsync(env, id, [.. "GIF89a"u8, 1, 0, 1, 0], token),
        };

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(JsonDocument.Parse(text).RootElement.GetProperty("errors").TryGetProperty("file", out _));
        Assert.Empty(storage.Puts);
    }

    [Fact]
    public async Task IU5_a_json_body_never_reaches_the_upload_action()
    {
        (IdentityTestEnv? env, RecordingImageStorage? storage, string? token) = await StartAsync();
        await using IdentityTestEnv _env = env;
        string id = (await AdminSamples.CreateProductAsync(env, token)).Str("id");

        (HttpStatusCode status, string text) = await SendAsync(env, id, new StringContent("{}", Encoding.UTF8, "application/json"), token);

        Assert.True(status == HttpStatusCode.NotFound, $"{status} {text}");
        Assert.Empty(storage.Puts);
    }

    [Fact]
    public async Task IU6_an_unknown_product_is_404_and_storage_is_not_touched()
    {
        (IdentityTestEnv? env, RecordingImageStorage? storage, string? token) = await StartAsync();
        await using IdentityTestEnv _env = env;

        (HttpStatusCode status, _) = await UploadAsync(env, Guid.NewGuid().ToString(), Png, token);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Empty(storage.Puts);
    }

    [Fact]
    public async Task IU7_blank_storage_config_is_503_without_any_network_call()
    {
        IdentityTestEnv env = await IdentityTestEnv.StartAsync(postgres);
        await using IdentityTestEnv _env = env;
        string token = await AdminSamples.AdminTokenAsync(env);
        string id = (await AdminSamples.CreateProductAsync(env, token)).Str("id");

        (HttpStatusCode status, string text) = await UploadAsync(env, id, Png, token).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Image storage is not configured", JsonDocument.Parse(text).RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task IU8_a_body_far_over_the_limit_is_rejected_by_the_framework()
    {
        (IdentityTestEnv? env, RecordingImageStorage? storage, string? token) = await StartAsync();
        await using IdentityTestEnv _env = env;
        string id = (await AdminSamples.CreateProductAsync(env, token)).Str("id");

        (HttpStatusCode status, _) = await UploadAsync(env, id, [.. Png, .. new byte[7 * 1024 * 1024]], token);

        Assert.Equal(HttpStatusCode.BadRequest, status); // observed once under TestServer; Kestrel answers 413
        Assert.Empty(storage.Puts);
    }

    [Fact]
    public async Task IU9_a_storage_failure_is_a_500_that_does_not_leak_the_exception_message()
    {
        (IdentityTestEnv? env, RecordingImageStorage? storage, string? token) = await StartAsync();
        await using IdentityTestEnv _env = env;
        string id = (await AdminSamples.CreateProductAsync(env, token)).Str("id");
        storage.Throw = new IOException("secret-bucket-detail");

        (HttpStatusCode status, string text) = await UploadAsync(env, id, Png, token);

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.DoesNotContain("secret-bucket-detail", text);
    }
}
