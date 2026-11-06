using Ecommerce.SharedKernel.Domain;

namespace Ecommerce.Identity.Domain;

internal sealed class User
{
    public const int MaxDisplayNameLength = 100;

    private User() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string SecurityStamp { get; private set; } = "";
    public UserRole Role { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Public registration. The role is never a parameter: registration can only create a Customer.</summary>
    public static User Register(Guid id, string email, string displayName, string passwordHash, string securityStamp, DateTimeOffset now) =>
        Create(id, email, displayName, passwordHash, securityStamp, UserRole.Customer, now);

    /// <summary>Startup bootstrap only (create-only; never promotes an existing account).</summary>
    public static User CreateAdmin(Guid id, string email, string displayName, string passwordHash, string securityStamp, DateTimeOffset now) =>
        Create(id, email, displayName, passwordHash, securityStamp, UserRole.Admin, now);

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is { } end && end > now;

    /// <summary>Counts a failed attempt. Returns true when this attempt locked the account.</summary>
    public bool RecordFailedLogin(DateTimeOffset now, LockoutPolicy policy)
    {
        AccessFailedCount++;
        UpdatedAt = now;
        if (AccessFailedCount < policy.MaxFailedAttempts)
        {
            return false;
        }
        LockoutEnd = now + policy.Duration;
        AccessFailedCount = 0;
        return true;
    }

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        AccessFailedCount = 0;
        LockoutEnd = null;
        UpdatedAt = now;
    }

    /// <summary>A credential change: new hash, new stamp, and any lockout is cleared.</summary>
    public void ReplacePasswordHash(string hash, string newStamp, DateTimeOffset now)
    {
        RequireValue(hash, "Password hash");
        RequireValue(newStamp, "Security stamp");
        PasswordHash = hash;
        SecurityStamp = newStamp;
        AccessFailedCount = 0;
        LockoutEnd = null;
        UpdatedAt = now;
    }

    /// <summary>Rehash with newer parameters; the password itself is unchanged, so the stamp stays.</summary>
    public void UpgradePasswordHash(string hash, DateTimeOffset now)
    {
        RequireValue(hash, "Password hash");
        PasswordHash = hash;
        UpdatedAt = now;
    }

    private static User Create(
        Guid id, string email, string displayName, string passwordHash, string securityStamp, UserRole role, DateTimeOffset now)
    {
        if (!EmailAddress.IsValid(email))
        {
            throw new DomainException("Email address is invalid.");
        }
        string name = (displayName ?? "").Trim();
        if (name.Length is 0 or > MaxDisplayNameLength)
        {
            throw new DomainException($"Display name must be 1 to {MaxDisplayNameLength} characters.");
        }
        RequireValue(passwordHash, "Password hash");
        RequireValue(securityStamp, "Security stamp");
        return new User
        {
            Id = id, Email = email, DisplayName = name, PasswordHash = passwordHash, SecurityStamp = securityStamp,
            Role = role, AccessFailedCount = 0, LockoutEnd = null, CreatedAt = now, UpdatedAt = now,
        };
    }

    private static void RequireValue(string? value, string what)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new DomainException($"{what} is required.");
        }
    }
}
