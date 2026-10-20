using Shameless.Application.Entities;
using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>L'état de la quête de rôle (l'objet de rôle) d'un joueur.</summary>
public enum EtatDeQuete
{
    /// <summary>L'objet de quête, pas encore transformé (objets 1200 à 1204).</summary>
    EnCours,

    /// <summary>La récompense de la quête (objets 1205 à 1208 et 1221).</summary>
    Terminee
}

/// <summary>L'objet de rôle d'un joueur : son identifiant du client et l'état de sa quête, null si l'objet n'est pas une quête connue.</summary>
public sealed record ObjetDeRole(int Id, EtatDeQuete? Etat)
{
    public static ObjetDeRole? De(int? id) => id is { } i and > 0
        ? new ObjetDeRole(i, i switch
        {
            >= 1200 and <= 1204 => EtatDeQuete.EnCours,
            >= 1205 and <= 1208 or 1221 => EtatDeQuete.Terminee,
            _ => null
        })
        : null;
}

/// <summary>
/// Ce que la page d'une game dit d'un joueur au-delà de ses stats : son rôle joué et sa source, ses
/// objets, son objet de rôle, ses sorts, ses runes et les valeurs dérivées. Tout est lu sur les valeurs
/// effectives (corrections d'un Administrateur comprises). Les identifiants sont ceux du client : les
/// noms et icônes viennent du catalogue Data Dragon.
/// </summary>
/// <param name="Items">Les sept emplacements (six objets et la babiole), 0 ou null pour un emplacement vide ; null sans objets.</param>
/// <param name="Perks">Les six runes ; null sans runes.</param>
/// <param name="CsParMinute">CS (sbires et monstres) par minute ; null sans durée ou sans CS.</param>
/// <param name="PartDesDegats">Part des dégâts aux champions de l'Équipe, de 0 à 1.</param>
/// <param name="PartDeLOr">Part de l'or gagné de l'Équipe, de 0 à 1.</param>
/// <param name="PartDesKills">Part des kills de l'Équipe (le joueur : ses kills seuls, pas les assistances), de 0 à 1.</param>
/// <param name="SortsLances">Les sorts lancés (Q W E R D F) ; null si le Rapport ne les porte pas.</param>
/// <param name="Pings">Les pings par type ; null si le Rapport ne les porte pas.</param>
public sealed record DetailsDuJoueur(
    Role? Role,
    SourceDuRole SourceDuRole,
    IReadOnlyList<int?>? Items,
    ObjetDeRole? ObjetDeRole,
    int? Spell1Id,
    int? Spell2Id,
    IReadOnlyList<int?>? Perks,
    int? PerkPrimaryStyle,
    int? PerkSubStyle,
    double? CsParMinute,
    double? OrParMinute,
    double? DegatsParMinute,
    double? VisionParMinute,
    double? PartDesDegats,
    double? PartDeLOr,
    double? PartDesKills,
    SortsLances? SortsLances = null,
    PingsDuJoueur? Pings = null)
{
    /// <summary>Les totaux de l'Équipe, sur les valeurs effectives de ses participants.</summary>
    internal sealed record TotauxDEquipe(int? Degats, int? Or, int? Kills)
    {
        public static TotauxDEquipe De(IEnumerable<StatsDuParticipant?> stats)
        {
            var liste = stats.Where(s => s is not null).Select(s => s!).ToList();
            int? Somme(Func<StatsDuParticipant, int?> champ) =>
                liste.Any(s => champ(s) is not null) ? liste.Sum(s => champ(s) ?? 0) : null;
            return new TotauxDEquipe(Somme(s => s.DamageToChampions), Somme(s => s.GoldEarned), Somme(s => s.Kills));
        }
    }

    internal static DetailsDuJoueur Calculer(RoleJoue role, StatsDuParticipant? stats, int? dureeSecondes, TotauxDEquipe equipe)
    {
        var minutes = dureeSecondes is > 0 ? dureeSecondes.Value / 60.0 : (double?)null;
        double? ParMinute(double? valeur) => valeur is { } v && minutes is { } m ? Math.Round(v / m, 2) : null;
        double? Part(int? valeur, int? total) => valeur is { } v && total is > 0 ? Math.Round((double)v / total.Value, 3) : null;

        return new DetailsDuJoueur(
            role.Role, role.Source,
            stats?.Items, ObjetDeRole.De(stats?.RoleBoundItem),
            stats?.Spell1Id, stats?.Spell2Id,
            stats?.Perks, stats?.PerkPrimaryStyle, stats?.PerkSubStyle,
            ParMinute(stats is null ? null : FormulesDeStats.Cs(stats)),
            ParMinute(stats?.GoldEarned),
            ParMinute(stats?.DamageToChampions),
            ParMinute(stats?.VisionScore),
            Part(stats?.DamageToChampions, equipe.Degats),
            Part(stats?.GoldEarned, equipe.Or),
            Part(stats?.Kills, equipe.Kills),
            stats?.SortsLances, stats?.Pings);
    }
}
