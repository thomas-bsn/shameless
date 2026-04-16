using Custom5v5.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Custom5v5.Application.BackgroundJobs;
public class RefreshRanksJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RefreshRanksJob> _logger;

    public RefreshRanksJob(IServiceProvider services, ILogger<RefreshRanksJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();

                _logger.LogInformation("Starting daily rank refresh");
            
                await playerService.RefreshAllRanksAsync();
                await playerService.RefreshRiotIdsAsync();

                if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Monday)
                {
                    _logger.LogInformation("Starting weekly main role refresh");
                    await playerService.RefreshAllMainRolesAsync();
                }
            
                _logger.LogInformation("Refresh completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshRanksJob failed");
            }

            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }
}