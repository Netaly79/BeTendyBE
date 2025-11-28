using BeTendlyBE.Data;
using BeTendlyBE.Domain;
using Microsoft.EntityFrameworkCore;

public class DailyBookingCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyBookingCleanupService> _logger;

    public DailyBookingCleanupService(IServiceScopeFactory scopeFactory, ILogger<DailyBookingCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Сервис ежедневной очистки бронирований запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var nextRun = now.Date.AddHours(3);
                if (nextRun <= now)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                _logger.LogInformation("Следующая очистка запланирована на {NextRun}", nextRun);

                await Task.Delay(delay, stoppingToken);

                await CleanupExpiredBookingsAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выполнении очистки просроченных бронирований");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task CleanupExpiredBookingsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var nowUtc = DateTime.UtcNow;

        var expired = await db.Bookings
            .Where(b => b.Status == BookingStatus.Pending && b.HoldExpiresUtc < nowUtc)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            _logger.LogInformation("Нет просроченных бронирований на {Time}", DateTime.Now);
            return;
        }

        foreach (var booking in expired)
        {
            booking.Status = BookingStatus.Cancelled;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Отменено {Count} просроченных бронирований", expired.Count);
    }
}
