using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shameless.Application.Services;

namespace Shameless.Application.BackgroundJobs;

/// <summary>Ferme la Soirée ouverte après une période sans game (<see cref="FermetureAutomatiqueSoireeOptions.AutoCloseAfter"/>).</summary>
public sealed class FermetureAutomatiqueSoireeJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FermetureAutomatiqueSoireeJob> _logger;
    private readonly IOptions<FermetureAutomatiqueSoireeOptions> _options;
    private readonly TimeProvider _clock;

    public FermetureAutomatiqueSoireeJob(
        IServiceProvider services,
        ILogger<FermetureAutomatiqueSoireeJob> logger,
        IOptions<FermetureAutomatiqueSoireeOptions> options,
        TimeProvider clock)
    {
        _services = services;
        _logger = logger;
        _options = options;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_options.Value.CheckInterval, _clock, ct);
            try
            {
                await FermerSiInactiveAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "FermetureAutomatiqueSoireeJob failed");
            }
        }
    }

    /// <summary>Un passage du job : ferme la Soirée ouverte si elle est inactive depuis le délai.</summary>
    public async Task FermerSiInactiveAsync()
    {
        using var scope = _services.CreateScope();
        var soirees = scope.ServiceProvider.GetRequiredService<SoireeService>();

        var fermee = await soirees.CloseIfIdleAsync(_options.Value.AutoCloseAfter);
        if (fermee != null)
            _logger.LogInformation("Soirée {SoireeId} fermée automatiquement", fermee.Id);
    }
}
