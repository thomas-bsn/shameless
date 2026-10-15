namespace Shameless.Application.BackgroundJobs;

/// <summary>Section de configuration <c>Soiree</c>.</summary>
public sealed class FermetureAutomatiqueSoireeOptions
{
    public const string Section = "Soiree";

    /// <summary>Période sans game au bout de laquelle la Soirée ouverte se ferme seule.</summary>
    public TimeSpan AutoCloseAfter { get; set; } = TimeSpan.FromHours(3);

    /// <summary>Fréquence de vérification du job.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);
}
