using Ecommerce.Identity.Application;
using Ecommerce.Identity.Domain;
using Ecommerce.Persistence;
using Ecommerce.SharedKernel.Audit;
using Ecommerce.SharedKernel.Persistence;
using Ecommerce.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Identity.Infrastructure.Bootstrap;

/// <summary>
/// D3. Create-only: creates the configured admin when no user has that email. If a non-admin already owns the email it
/// logs an error (user id only) and does NOT promote, because registration is open and emails are unverified: "promote
/// this email" would let whoever registers it first become Admin. It never demotes.
/// </summary>
internal sealed class AdminBootstrapHostedService(
    IServiceScopeFactory scopes,
    IOptions<AdminBootstrapOptions> options,
    ILogger<AdminBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        AdminBootstrapOptions settings = options.Value;
        if (!settings.IsConfigured)
        {
            return;
        }

        try
        {
            using IServiceScope scope = scopes.CreateScope();
            EcommerceDbContext db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
            if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            {
                logger.LogWarning("Admin bootstrap skipped: migrations are pending. Run dotnet-ef database update.");
                return;
            }

            string email = EmailAddress.Normalize(settings.AdminEmail);
            IUserRepository users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            User? existing = await users.FindByEmailAsync(email, cancellationToken);
            if (existing is not null)
            {
                if (existing.Role != UserRole.Admin)
                {
                    logger.LogError(
                        "Admin bootstrap: user {UserId} owns the configured admin email but is not an Admin. It was not promoted.",
                        existing.Id);
                }
                return;
            }

            if (settings.AdminPassword.Length is < PasswordPolicy.MinLength or > PasswordPolicy.MaxLength)
            {
                // Same policy as register and reset. Names the config key only: never the email or the password.
                logger.LogError(
                    "Admin bootstrap skipped: Identity:Bootstrap:AdminPassword must be {Min} to {Max} characters. No admin was created.",
                    PasswordPolicy.MinLength, PasswordPolicy.MaxLength);
                return;
            }

            IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();
            IPasswordHashing hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashing>();
            ISecureTokenGenerator generator = scope.ServiceProvider.GetRequiredService<ISecureTokenGenerator>();
            DateTimeOffset now = clock.UtcNow;
            User admin = User.CreateAdmin(
                Guid.CreateVersion7(now), email, "Administrator", hashing.Hash(settings.AdminPassword), generator.NewSecurityStamp(), now);
            users.Add(admin);
            // Rule 13: creating an Admin is audited. No email in the row; it commits with the user.
            scope.ServiceProvider.GetRequiredService<IAuditLog>().Add(
                new AuditEntry(null, "identity.admin-bootstrap", "User", admin.Id, null, "{\"role\":\"Admin\"}", null, now));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
            logger.LogInformation("Admin bootstrap created admin user {UserId}", admin.Id);
        }
        catch (UniqueConstraintViolationException)
        {
            // Another instance created it first. Nothing to do.
            logger.LogInformation("Admin bootstrap: the admin user already exists.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Admin bootstrap failed; the API continues without it.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
