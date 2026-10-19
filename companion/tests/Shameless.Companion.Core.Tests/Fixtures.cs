namespace Shameless.Companion.Core.Tests;

/// <summary>Les captures réelles (anonymisées) de companion/fixtures, copiées à côté des tests.</summary>
internal static class Fixtures
{
    public const string CustomContreBots = "custom-contre-bots-5v5";

    public static string Eog(string dossier = CustomContreBots) => Lire(dossier, "eog-stats-block.json");
    public static string Historique(string dossier = CustomContreBots) => Lire(dossier, "match-history-game.json");

    private static string Lire(string dossier, string fichier) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "lcu", dossier, fichier));
}
