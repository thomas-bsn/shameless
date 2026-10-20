using System.Text.Json;
using Shameless.Application.Entities;

namespace Shameless.Application.Stats;

/// <summary>Le badge qu'un joueur porte pour une game (à ne pas confondre avec le MVP voté).</summary>
public enum BadgeDeGame
{
    /// <summary>La meilleure note de l'Équipe gagnante.</summary>
    MeilleurePerf,

    /// <summary>La meilleure note de l'Équipe perdante.</summary>
    Ace
}

/// <summary>Ce que la game dit d'un joueur : note, détails, place dans la game et badge.</summary>
/// <param name="Place">Sa place de 1 à 10 d'après sa note ; null sans note.</param>
public sealed record LectureDuJoueur(NoteDePerformance? Note, DetailsDuJoueur Details, int? Place, BadgeDeGame? Badge);

/// <summary>Les objectifs d'une Équipe, lus dans <c>donnees.teams</c> du Rapport ; chaque champ est null quand le client ne l'a pas donné.</summary>
public sealed record ObjectifsDEquipe(
    int? DragonKills, int? BaronKills, int? RiftHeraldKills, int? HordeKills, int? TowerKills, int? InhibitorKills,
    bool? FirstBlood, bool? FirstTower, bool? FirstInhibitor)
{
    /// <summary>Les objectifs de l'Équipe jouant le <paramref name="side"/> (« blue » = teamId 100, « red » = 200) ; null sans données pour elle.</summary>
    public static ObjectifsDEquipe? Lire(string? riotMatchData, string side)
    {
        if (string.IsNullOrWhiteSpace(riotMatchData)) return null;
        var teamId = side == Sides.Blue ? 100 : 200;
        try
        {
            using var document = JsonDocument.Parse(riotMatchData);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("teams", out var equipes)
                || equipes.ValueKind != JsonValueKind.Array) return null;
            foreach (var equipe in equipes.EnumerateArray())
            {
                if (equipe.ValueKind != JsonValueKind.Object
                    || !equipe.TryGetProperty("teamId", out var id) || !id.TryGetInt32(out var n) || n != teamId) continue;
                return new ObjectifsDEquipe(
                    Entier(equipe, "dragonKills"), Entier(equipe, "baronKills"), Entier(equipe, "riftHeraldKills"),
                    Entier(equipe, "hordeKills"), Entier(equipe, "towerKills"), Entier(equipe, "inhibitorKills"),
                    Booleen(equipe, "firstBlood"), Booleen(equipe, "firstTower"), Booleen(equipe, "firstInhibitor"));
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? Entier(JsonElement objet, string nom) =>
        objet.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    private static bool? Booleen(JsonElement objet, string nom) =>
        objet.TryGetProperty(nom, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
}

/// <summary>
/// La lecture d'une game pour la page de la game : pour chaque participant sa note, ses détails, sa
/// place et son badge. Un participant sans note n'a ni place ni badge.
/// </summary>
public static class LectureDeLaGame
{
    /// <summary>Par PlayerId. La game est chargée avec ses participants, leurs joueurs, leurs corrections et sa Série (joueurs).</summary>
    public static IReadOnlyDictionary<int, LectureDuJoueur> Calculer(CustomGame game)
    {
        var notes = NotesDeLaGame.Calculer(game);
        var duree = NotesDeLaGame.DureeDe(game);
        var stats = game.Participants.ToDictionary(p => p.PlayerId, ValeursEffectives.Stats);
        var totaux = game.Participants.Select(p => p.Side).Distinct().ToDictionary(
            side => side,
            side => DetailsDuJoueur.TotauxDEquipe.De(game.Participants.Where(p => p.Side == side).Select(p => stats[p.PlayerId])));

        var classes = game.Participants
            .Where(p => notes[p.PlayerId] is not null)
            .OrderByDescending(p => notes[p.PlayerId]!.Note)
            .ThenBy(p => p.Id)
            .ToList();

        BadgeDeGame? BadgeDe(CustomGameParticipant p)
        {
            if (game.WinningTeam is null) return null;
            var meilleur = classes.FirstOrDefault(c => game.EquipeOn(c.Side) == game.EquipeOn(p.Side));
            return meilleur?.PlayerId != p.PlayerId ? null
                : game.EquipeOn(p.Side) == game.WinningTeam ? BadgeDeGame.MeilleurePerf : BadgeDeGame.Ace;
        }

        return game.Participants.ToDictionary(p => p.PlayerId, p =>
        {
            var rang = classes.IndexOf(p);
            var details = DetailsDuJoueur.Calculer(NotesDeLaGame.RoleJoueDe(game, p, stats[p.PlayerId]), stats[p.PlayerId], duree, totaux[p.Side]);
            return new LectureDuJoueur(notes[p.PlayerId], details, rang >= 0 ? rang + 1 : null, rang >= 0 ? BadgeDe(p) : null);
        });
    }
}
