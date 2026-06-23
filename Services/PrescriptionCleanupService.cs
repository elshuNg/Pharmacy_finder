using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmacyFinder.API.Data;
using PharmacyFinder.API.Helpers;
using PharmacyFinder.API.Models;

namespace PharmacyFinder.API.Services;

public class PrescriptionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<PrescriptionSettings> options,
    ILogger<PrescriptionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.CleanupIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStuckPrescriptionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Prescription cleanup failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CleanupStuckPrescriptionsAsync(CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMinutes(Math.Max(1, options.Value.ProcessingTimeoutMinutes));
        var cutoff = DateTime.UtcNow - timeout;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updated = await db.Prescriptions
            .Where(p => p.Status == PrescriptionStatus.Processing && p.UploadedAt < cutoff)
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.Status, PrescriptionStatus.Failed),
                cancellationToken);

        if (updated > 0)
            logger.LogInformation("Marked {Count} stuck prescription(s) as Failed.", updated);
    }
}
