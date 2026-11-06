using System.Security.Cryptography;
using Ecommerce.Identity.Domain;
using Ecommerce.Identity.Infrastructure.Jwt;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Identity.Tests.Infrastructure;

// ID-9: the access token's contents, signature and the signing-key rules.
public class JwtTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "t";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static JwtOptions Options(string? key = null) => new()
    {
        Issuer = "ecommerce-api",
        Audience = "ecommerce-store",
        SigningKey = key ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    };

    private static JwtSigningKeyProvider Keys(JwtOptions o, string env = "Testing") =>
        new(Microsoft.Extensions.Options.Options.Create(o), new Env(env), NullLogger<JwtSigningKeyProvider>.Instance);

    private static User UserWithRole(UserRole role)
    {
        User user = User.Register(Guid.CreateVersion7(Now), "ana@example.test", "Ana", "hash", "00112233445566778899aabbccddeeff", Now);
        typeof(User).GetProperty(nameof(User.Role))!.SetValue(user, role);
        return user;
    }

    private static TokenValidationParameters Validation(SecurityKey key) => new()
    {
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        IssuerSigningKey = key,
        ValidIssuer = "ecommerce-api",
        ValidAudience = "ecommerce-store",
        ValidateLifetime = false,
    };

    [Fact]
    public async Task Token_carries_sub_role_jti_iss_aud_is_HS256_and_expires_15_minutes_after_now()
    {
        JwtOptions options = Options();
        JwtSigningKeyProvider keys = Keys(options);
        User user = UserWithRole(UserRole.Admin);

        (string? token, DateTimeOffset expiresAt) = new JwtAccessTokenIssuer(keys, Microsoft.Extensions.Options.Options.Create(options)).Issue(user, Now);

        Assert.Equal(Now.AddMinutes(15), expiresAt);
        JsonWebToken jwt = new JsonWebToken(token);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
        Assert.Equal(user.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("Admin", jwt.GetClaim("role").Value);
        Assert.NotEmpty(jwt.GetClaim("jti").Value);
        Assert.Equal("ecommerce-api", jwt.Issuer);
        Assert.Contains("ecommerce-store", jwt.Audiences);
        Assert.Equal(Now.UtcDateTime, jwt.ValidFrom);
        Assert.Equal(Now.AddMinutes(15).UtcDateTime, jwt.ValidTo);
        Assert.Equal(Now.UtcDateTime, jwt.IssuedAt);

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Validation(keys.Key));
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_token_validates_only_against_the_key_that_signed_it()
    {
        JwtOptions options = Options();
        (string? token, DateTimeOffset _) = new JwtAccessTokenIssuer(Keys(options), Microsoft.Extensions.Options.Options.Create(options)).Issue(UserWithRole(UserRole.Customer), Now);
        SymmetricSecurityKey other = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));

        Assert.False((await new JsonWebTokenHandler().ValidateTokenAsync(token, Validation(other))).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !!")]
    public void A_missing_or_malformed_key_is_invalid_outside_development_and_the_provider_refuses_to_start(string key)
    {
        JwtOptions o = Options(key);

        Assert.False(JwtOptionsValidation.IsValid(o, new Env("Production")));
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Keys(o, "Production"));
        Assert.Contains("Identity:Jwt:SigningKey", ex.Message);
    }

    [Fact]
    public void A_key_shorter_than_32_bytes_is_invalid_even_in_development()
    {
        JwtOptions o = Options(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));

        Assert.False(JwtOptionsValidation.IsValid(o, new Env("Development")));
        Assert.False(JwtOptions.TryDecodeKey(o.SigningKey, out _));
        Assert.True(JwtOptions.TryDecodeKey(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), out _));
    }

    [Fact]
    public void In_development_an_empty_key_is_valid_and_gives_one_stable_ephemeral_key_per_provider()
    {
        JwtOptions o = Options("");
        Assert.True(JwtOptionsValidation.IsValid(o, new Env("Development")));

        JwtSigningKeyProvider first = Keys(o, "Development");

        Assert.Same(first.Key, first.Key);
        Assert.True(((SymmetricSecurityKey)first.Key).Key.Length >= 32);
        Assert.NotEqual(((SymmetricSecurityKey)first.Key).Key, ((SymmetricSecurityKey)Keys(o, "Development").Key).Key);
    }
}
