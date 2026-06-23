using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmacyFinder.API.Helpers;
using PharmacyFinder.API.Models;
using PharmacyFinder.API.Services;

namespace PharmacyFinder.API.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();

        var bootstrap = scope.ServiceProvider
            .GetRequiredService<IOptions<BootstrapAdminSettings>>().Value;

        if (string.IsNullOrWhiteSpace(bootstrap.Email) || string.IsNullOrWhiteSpace(bootstrap.Password))
            return;

        if (!EmailValidator.IsValid(bootstrap.Email))
        {
            logger.LogWarning("BootstrapAdmin email is invalid; skipping admin seed.");
            return;
        }

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
            return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var normalizedEmail = bootstrap.Email.Trim().ToLowerInvariant();

        db.Users.Add(new User
        {
            Email = normalizedEmail,
            PasswordHash = hasher.HashPassword(bootstrap.Password),
            FullName = string.IsNullOrWhiteSpace(bootstrap.FullName) ? "System Admin" : bootstrap.FullName.Trim(),
            Role = UserRole.Admin,
            IsActive = true
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Bootstrap admin account created for {Email}.", normalizedEmail);
    }
}
