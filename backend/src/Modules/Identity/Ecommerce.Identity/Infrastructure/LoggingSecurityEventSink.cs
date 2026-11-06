using Ecommerce.Identity.Application;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Identity.Infrastructure;

/// <summary>Structured Warning logs: ids only, never a token, password or email. The future audit/alert hook.</summary>
internal sealed class LoggingSecurityEventSink(ILogger<LoggingSecurityEventSink> logger) : ISecurityEventSink
{
    public void RefreshTokenReuseDetected(Guid userId, Guid familyId) =>
        logger.LogWarning("Refresh token reuse detected; family {FamilyId} of user {UserId} revoked", familyId, userId);

    public void AccountLockedOut(Guid userId) =>
        logger.LogWarning("User {UserId} locked out", userId);
}
