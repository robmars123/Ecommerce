using System.Buffers.Text;
using System.Security.Cryptography;
using Ecommerce.Identity.Application;

namespace Ecommerce.Identity.Infrastructure;

internal sealed class RandomTokenGenerator : ISecureTokenGenerator
{
    public string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public string NewSecurityStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
