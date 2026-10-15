using Shameless.Application.Picker;

namespace Shameless.Application.Entities;

/// <summary>
/// La session de Picker en cours d'une Soirée : les dix joueurs cochés, la méthode choisie et
/// l'Équipe de chaque joueur dans le tirage courant. Au plus une par Soirée. Une fois validée, elle
/// produit les deux Équipes d'une Série et disparaît.
/// </summary>
/// <remarks>
/// La Draft porte ses capitaines et l'ordre des picks sur <see cref="PickerSessionPlayer"/> et le
/// premier pick sur la session ; l'Équipe au trait s'en déduit. Elle remplit
/// <see cref="PickerSessionPlayer.Equipe"/> comme la Roulette : la validation est la même.
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

    /// <summary>L'Équipe qui choisit en premier dans la Draft ("A" | "B").</summary>
    public string? PremierPick { get; set; }

    /// <summary>
    /// Jeton de concurrence : chaque commande sur la session l'incrémente (<see cref="Touch"/>) ; son
    /// enregistrement est refusé si une commande simultanée l'a incrémenté entre-temps.
    /// </summary>
    public int Version { get; set; }

    /// <summary>Marque la session comme modifiée par la commande en cours (voir <see cref="Version"/>).</summary>
    public void Touch() => Version++;

    /// <summary>Les dix joueurs cochés, dans l'ordre de la sélection.</summary>
    public List<PickerSessionPlayer> Players { get; set; } = [];

    /// <summary>Les deux Équipes sont complètes : la session peut créer la Série.</summary>
    public bool CanValidate =>
        Players.Count(p => p.Equipe == Equipes.A) == Roulette.PlayersPerTeam &&
        Players.Count(p => p.Equipe == Equipes.B) == Roulette.PlayersPerTeam;

    /// <summary>Le capitaine de l'Équipe dans la Draft, ou null.</summary>
    public int? CapitaineOf(string equipe) =>
        Players.SingleOrDefault(p => p.Capitaine && p.Equipe == equipe)?.PlayerId;

    /// <summary>Les picks faits dans la Draft, dans l'ordre.</summary>
    public IReadOnlyList<PickerSessionPlayer> Picks =>
        Players.Where(p => p.PickOrder != null).OrderBy(p => p.PickOrder).ToList();

    /// <summary>L'Équipe qui doit choisir dans la Draft ; null hors Draft ou quand elle est complète.</summary>
    public string? EquipeAuTrait =>
        Methode == PickerMethode.Draft && PremierPick != null
            ? Draft.EquipeAuTrait(PremierPick, Players.Count(p => p.PickOrder != null))
            : null;

    /// <summary>Remplace les dix joueurs cochés ; les équipes sont à reformer.</summary>
    public void Select(IEnumerable<int> playerIds)
    {
        Methode = null;
        Variante = null;
        PremierPick = null;
        Players.Clear();
        Players.AddRange(playerIds.Select((id, i) => new PickerSessionPlayer { PlayerId = id, Position = i }));
    }

    /// <summary>Retient un tirage de Roulette comme composition courante.</summary>
    public void ApplyRoulette(RouletteVariante variante, Composition composition)
    {
        Methode = PickerMethode.Roulette;
        Variante = variante;
        PremierPick = null;
        foreach (var player in Players)
        {
            player.Capitaine = false;
            player.PickOrder = null;
            player.Equipe = composition.TeamA.Contains(player.PlayerId) ? Equipes.A
                : composition.TeamB.Contains(player.PlayerId) ? Equipes.B
                : null;
            // Nouvelles Équipes : les rôles de l'Autorole sont à refaire.
            player.Role = null;
        }
    }

    /// <summary>Lance (ou relance) une Draft : chaque capitaine ouvre son Équipe, aucun pick n'est fait.</summary>
    public void StartDraft(Capitaines capitaines, string premierPick)
    {
        Methode = PickerMethode.Draft;
        Variante = null;
        PremierPick = premierPick;
        foreach (var player in Players)
        {
            player.Capitaine = player.PlayerId == capitaines.CapitaineA || player.PlayerId == capitaines.CapitaineB;
            player.PickOrder = null;
            player.Equipe = player.PlayerId == capitaines.CapitaineA ? Equipes.A
                : player.PlayerId == capitaines.CapitaineB ? Equipes.B
                : null;
            player.Role = null;
        }
    }

    /// <summary>L'Équipe au trait prend un des dix joueurs encore libre.</summary>
    public void Pick(int playerId)
    {
        var equipe = EquipeAuTrait ?? throw new InvalidOperationException("Aucune Équipe n'est au trait.");
        var player = Players.Single(p => p.PlayerId == playerId);
        player.PickOrder = Picks.Count;
        player.Equipe = equipe;
        // Les Équipes changent : les rôles de l'Autorole sont à refaire.
        foreach (var p in Players)
            p.Role = null;
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

    /// <summary>Le joueur est le capitaine de son Équipe dans la Draft.</summary>
    public bool Capitaine { get; set; }

    /// <summary>Rang du pick qui l'a pris dans la Draft (0 pour le premier), null s'il n'a pas été pris.</summary>
    public int? PickOrder { get; set; }

    /// <summary>Le rôle attribué par l'Autorole, null tant qu'il n'a pas été lancé.</summary>
    public Role? Role { get; set; }
}

public enum PickerMethode
{
    Roulette,
    Draft
}

/// <summary>Comment les deux capitaines de la Draft sont désignés.</summary>
public enum DesignationCapitaines
{
    Manuels,
    AuHasard,
    PlusGrosCosts
}

public enum RouletteVariante
{
    Aleatoire,
    Equilibree
}
