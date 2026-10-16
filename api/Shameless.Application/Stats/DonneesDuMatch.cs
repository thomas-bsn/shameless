namespace Shameless.Application.Stats;

/// <summary>
/// Les données d'une partie lues dans les données Riot brutes envoyées par le Companion
/// (<c>CustomGame.RiotMatchData</c>, conservées telles quelles). Chaque champ est null quand la source
/// ne le fournit pas.
/// </summary>
/// <param name="DurationSeconds">La durée de la partie en secondes (<c>gameDuration</c>), base des stats par minute.</param>
/// <param name="GameMode">Le mode de jeu (<c>gameMode</c>, ex. « CLASSIC »).</param>
/// <param name="GameType">Le type de partie (<c>gameType</c>, ex. « CUSTOM_GAME »).</param>
/// <param name="GameVersion">La version du jeu (<c>gameVersion</c>, ex. « 14.20.123.4567 »).</param>
public sealed record DonneesDuMatch(int? DurationSeconds, string? GameMode, string? GameType, string? GameVersion)
{
    /// <summary>Les données lues dans le JSON brut de la partie, null sans données Riot (saisie manuelle).</summary>
    public static DonneesDuMatch? Lire(string? riotMatchData)
    {
        if (ChampsLcu.De(riotMatchData) is not { } champs) return null;
        return new DonneesDuMatch(
            DurationSeconds: champs.Entier("gameDuration", "gameLength", "GAME_LENGTH"),
            GameMode: champs.Texte("gameMode", "GAME_MODE"),
            GameType: champs.Texte("gameType", "GAME_TYPE"),
            GameVersion: champs.Texte("gameVersion", "GAME_VERSION"));
    }
}
