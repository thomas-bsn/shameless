namespace Shameless.Application.BackgroundJobs;

public sealed class RefreshRanksJobOptions
{
    /// <summary>Faux quand aucune clé Riot n'est configurée.</summary>
    public bool Enabled { get; set; }
}
