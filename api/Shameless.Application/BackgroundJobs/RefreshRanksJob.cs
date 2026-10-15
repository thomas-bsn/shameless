using Shameless.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shameless.Application.BackgroundJobs;
public class RefreshRanksJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RefreshRanksJob> _logger;
    private readonly RefreshRanksJobOptions _options;
    private readonly TimeProvider _clock;

    public RefreshRanksJob(IServiceProvider services, ILogger<RefreshRanksJob> logger, IOptions<RefreshRanksJobOptions> options, TimeProvider clock)
    {
        _services = services;
        _logger = logger;
        _options = options.Value;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("No Riot API key configured: rank refresh disabled");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var playerService = scope.ServiceProvider.GetRequiredService<IPlayerService>();

                _logger.LogInformation("Starting daily rank refresh");
            
                await playerService.RefreshAllRanksAsync();
                await playerService.RefreshRiotIdsAsync();

                if (_clock.GetUtcNow().DayOfWeek == DayOfWeek.Monday)
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

            await Task.Delay(TimeSpan.FromHours(24), _clock, ct);
        }
    }
}