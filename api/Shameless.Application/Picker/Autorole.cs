using Shameless.Application.Interfaces;

namespace Shameless.Application.Picker;

/// <summary>Un rôle de la Faille : chaque Équipe en a exactement un de chaque.</summary>
public enum Role
{
    Top,
    Jungle,
    Mid,
    Adc,
    Support
}

public enum AutoroleVariante
{
    HorsRole,
    Aleatoire
}

/// <summary>
/// L'Autorole du Picker, sans base ni HTTP : l'aléa est injecté. Attribue à chaque joueur des deux
/// Équipes un rôle, chaque Équipe recevant un top, un jungle, un mid, un ADC et un support ; ne
/// change jamais la composition des Équipes.
/// </summary>
public static class Autorole
{
    public static readonly IReadOnlyList<Role> Roles = [Role.Top, Role.Jungle, Role.Mid, Role.Adc, Role.Support];

    /// <summary>Le rôle principal d'un joueur d'après sa position Riot (TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY), ou null.</summary>
    public static Role? RolePrincipal(string? teamPosition) => teamPosition switch
    {
        "TOP" => Role.Top,
        "JUNGLE" => Role.Jungle,
        "MIDDLE" => Role.Mid,
        "BOTTOM" => Role.Adc,
        "UTILITY" => Role.Support,
        _ => null
    };

    public static IReadOnlyDictionary<int, Role> Attribuer(
        AutoroleVariante variante,
        Composition composition,
        IReadOnlyDictionary<int, Role?> rolesPrincipaux,
        IRandomSource random)
    {
        var roles = new Dictionary<int, Role>();
        foreach (var equipe in new[] { composition.TeamA, composition.TeamB })
        {
            var permutation = variante == AutoroleVariante.HorsRole
                ? HorsRole(equipe, rolesPrincipaux, random)
                : Melanger(Roles, random);
            for (var i = 0; i < equipe.Count; i++)
                roles[equipe[i]] = permutation[i];
        }
        return roles;
    }

    /// <summary>
    /// Tirage uniforme parmi les attributions de l'Équipe qui mettent le moins de joueurs sur leur
    /// rôle principal : aucun quand c'est possible.
    /// </summary>
    private static Role[] HorsRole(IReadOnlyList<int> equipe, IReadOnlyDictionary<int, Role?> rolesPrincipaux, IRandomSource random)
    {
        var attributions = Permutations(Roles)
            .Select(p => (Roles: p, SurRolePrincipal: equipe.Where((id, i) => rolesPrincipaux.GetValueOrDefault(id) == p[i]).Count()))
            .ToList();
        var minimum = attributions.Min(a => a.SurRolePrincipal);
        var candidates = attributions.Where(a => a.SurRolePrincipal == minimum).ToList();
        return candidates[random.Next(candidates.Count)].Roles;
    }

    private static IEnumerable<Role[]> Permutations(IReadOnlyList<Role> roles)
    {
        if (roles.Count == 0)
        {
            yield return [];
            yield break;
        }
        for (var i = 0; i < roles.Count; i++)
        {
            var reste = roles.Where((_, j) => j != i).ToList();
            foreach (var suite in Permutations(reste))
                yield return [roles[i], .. suite];
        }
    }

    private static Role[] Melanger(IReadOnlyList<Role> roles, IRandomSource random)
    {
        var melange = roles.ToArray();
        for (var i = melange.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (melange[i], melange[j]) = (melange[j], melange[i]);
        }
        return melange;
    }
}
