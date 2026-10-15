using Shameless.Application.Picker;

namespace Shameless.Application.Entities;

/// <summary>
/// La session de Picker en cours d'une Soirée : les dix joueurs cochés, la méthode choisie et
/// l'Équipe de chaque joueur dans le tirage courant. Au plus une par Soirée. Une fois validée, elle
/// produit les deux Équipes d'une Série et disparaît.
/// </summary>
/// <remarks>
/// Les étapes suivantes s'y ajoutent sans la refondre : la Draft porte ses capitaines, le premier
/// pick et l'ordre des picks sur <see cref="PickerSessionPlayer"/> (le tour courant s'en déduit),
/// l'Autorole porte le rôle de chaque joueur au même endroit.
/// </remarks>
public class PickerSession
{
    public int Id { get; set; }
    public int SoireeId { get; set; }
    public Soiree Soiree { get; set; } = null!;

    /// <summary>Draft ou Roulette ; null tant que les équipes ne sont pas en cours de formation.</summary>
    public PickerMethode? Methode { get; set; }

    /// <summary>La variante de la Roulette, quand c'est la méthode choisie.</summary>
    public RouletteVariante? Variante { get; set; }

    /// <summary>Les dix joueurs cochés, dans l'ordre de la sélection.</summary>
    public List<PickerSessionPlayer> Players { get; set; } = [];

    /// <summary>Les deux Équipes sont complètes : la session peut créer la Série.</summary>
    public bool CanValidate =>
        Players.Count(p => p.Equipe == Equipes.A) == Roulette.PlayersPerTeam &&
        Players.Count(p => p.Equipe == Equipes.B) == Roulette.PlayersPerTeam;

    /// <summary>Remplace les dix joueurs cochés ; les équipes sont à reformer.</summary>
    public void Select(IEnumerable<int> playerIds)
    {
        Methode = null;
        Variante = null;
        Players.Clear();
        Players.AddRange(playerIds.Select((id, i) => new PickerSessionPlayer { PlayerId = id, Position = i }));
    }

    /// <summary>Retient un tirage de Roulette comme composition courante.</summary>
    public void ApplyRoulette(RouletteVariante variante, Composition composition)
    {
        Methode = PickerMethode.Roulette;
        Variante = variante;
        foreach (var player in Players)
        {
            player.Equipe = composition.TeamA.Contains(player.PlayerId) ? Equipes.A
                : composition.TeamB.Contains(player.PlayerId) ? Equipes.B
                : null;
            // Nouvelles Équipes : les rôles de l'Autorole sont à refaire.
            player.Role = null;
        }
    }

    /// <summary>Retient une attribution de l'Autorole ; la composition des Équipes ne change pas.</summary>
    public void ApplyAutorole(IReadOnlyDictionary<int, Role> roles)
    {
        foreach (var player in Players)
            player.Role = roles.TryGetValue(player.PlayerId, out var role) ? role : null;
    }

    /// <summary>La composition courante des deux Équipes.</summary>
    public Composition Composition => new(PlayerIdsOf(Equipes.A), PlayerIdsOf(Equipes.B));

    public IReadOnlyList<int> PlayerIdsOf(string equipe) =>
        Players.OrderBy(p => p.Position).Where(p => p.Equipe == equipe).Select(p => p.PlayerId).ToList();
}

/// <summary>Un des dix joueurs cochés d'une session de Picker.</summary>
public class PickerSessionPlayer
{
    public int Id { get; set; }
    public int PickerSessionId { get; set; }
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>Rang dans la sélection.</summary>
    public int Position { get; set; }

    /// <summary>L'Équipe du joueur dans la composition courante ("A" | "B"), null s'il n'est pas encore placé.</summary>
    public string? Equipe { get; set; }

    /// <summary>Le rôle attribué par l'Autorole, null tant qu'il n'a pas été lancé.</summary>
    public Role? Role { get; set; }
}

public enum PickerMethode
{
    Roulette
}

public enum RouletteVariante
{
    Aleatoire,
    Equilibree
}
