using Shameless.Application.Companion;
using Shameless.Application.Entities;
using Shameless.Application.Stats;

namespace Shameless.Application.DTOs.Match;

/// <summary>
/// La chronologie d'une game (ADR 0022) : ses séries avec leur nature, ses points datés par joueur et ses
/// événements d'objectifs. Absente (null dans la game) quand le Rapport n'en portait pas.
/// </summary>
public sealed record ChronologieDeGameResponse(
    List<SerieDeChronologieDto> Series,
    List<PointDeChronologieDto> Points,
    List<EvenementDeChronologieDto> Evenements)
{
    public const string Reelle = "reelle";
    public const string Estimee = "estimee";
    public const string Indisponible = "indisponible";

    /// <summary>La chronologie de la game, null sans chronologie. Demande les participants chargés.</summary>
    public static ChronologieDeGameResponse? From(CustomGame game)
    {
        if (ChronologieDeGame.Lire(game.Chronologie) is not { Points: { } points } chronologie) return null;

        var parPuuid = game.Participants.ToDictionary(p => p.Player.PUUID);
        var orFinal = game.Participants.ToDictionary(p => p.Player.PUUID, p => ValeursEffectives.Stats(p)?.GoldEarned);
        // La valeur d'objets du dernier point de chaque joueur : la base de la remise à l'échelle.
        var derniereValeur = points
            .SelectMany(p => p.Joueurs!)
            .GroupBy(j => j.Puuid!)
            .ToDictionary(g => g.Key, g => g.Last().ItemsValue!.Value);

        int? OrEstime(JoueurDuPoint j)
        {
            if (orFinal.GetValueOrDefault(j.Puuid!) is not { } or || derniereValeur[j.Puuid!] <= 0) return null;
            return (int)Math.Round((double)j.ItemsValue!.Value * or / derniereValeur[j.Puuid!]);
        }

        var dtos = points.Select(p => new PointDeChronologieDto(
            p.T!.Value,
            p.Joueurs!.Where(j => parPuuid.ContainsKey(j.Puuid!)).Select(j => new JoueurDuPointDto(
                parPuuid[j.Puuid!].PlayerId, j.Kills!.Value, j.Deaths!.Value, j.Assists!.Value, j.Cs!.Value,
                j.Level!.Value, j.Vision!.Value, j.ItemsValue!.Value, j.Dead!.Value, OrEstime(j))).ToList()))
            .ToList();
        var orEstimable = dtos.Any(p => p.Joueurs.Any(j => j.OrEstime != null));

        return new ChronologieDeGameResponse(
            [
                new("kills", Reelle), new("morts", Reelle), new("assists", Reelle), new("cs", Reelle),
                new("niveau", Reelle), new("vision", Reelle), new("objectifs", Reelle),
                new("or", orEstimable ? Estimee : Indisponible),
                new("degats", Indisponible), new("xp", Indisponible)
            ],
            dtos,
            (chronologie.Evenements ?? []).Select(e => new EvenementDeChronologieDto(e.T!.Value, e.Type!, e.Side!)).ToList());
    }
}

/// <param name="Nom">kills, morts, assists, cs, niveau, vision, objectifs, or, degats ou xp.</param>
/// <param name="Nature">« reelle » (donnée par la partie), « estimee » (calculée par l'API) ou « indisponible » (non fournie en custom).</param>
public sealed record SerieDeChronologieDto(string Nom, string Nature);

/// <param name="T">Les secondes depuis le début de la partie.</param>
public sealed record PointDeChronologieDto(int T, List<JoueurDuPointDto> Joueurs);

/// <param name="ValeurObjets">La valeur totale des objets du joueur, en or.</param>
/// <param name="Mort">Vrai si le joueur est mort à cet instant.</param>
/// <param name="OrEstime">L'or total estimé : la valeur des objets remise à l'échelle sur l'or total final du joueur ; null si son or final ou sa valeur d'objets finale manque.</param>
public sealed record JoueurDuPointDto(
    int PlayerId, int Kills, int Deaths, int Assists, int Cs, int Niveau, int Vision, int ValeurObjets, bool Mort, int? OrEstime);

/// <param name="Type">dragon, herald, baron, turret ou inhibitor.</param>
/// <param name="Side">Le side (« blue » ou « red ») de l'équipe qui a pris l'objectif.</param>
public sealed record EvenementDeChronologieDto(int T, string Type, string Side);
