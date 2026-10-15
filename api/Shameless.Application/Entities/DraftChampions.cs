using Shameless.Application.ChampionDraft;

namespace Shameless.Application.Entities;

/// <summary>
/// La Draft de champions d'une game : les bans puis les picks des deux Équipes, dans l'ordre fixe du
/// mode Tournament (<see cref="OrdreTournament"/>) selon le Side de chaque Équipe pour cette game.
/// Les picks sont des champions de l'Équipe, sans joueur associé. Elle reste sur la game une fois
/// terminée, y compris après la fin de la Série.
/// </summary>
public class DraftChampions
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public CustomGame Game { get; set; } = null!;

    /// <summary>Quand le tour courant a commencé : à l'ouverture, puis à chaque action jouée.</summary>
    public DateTime TourCommenceLe { get; set; }

    /// <summary>
    /// Jeton de concurrence : chaque action l'incrémente ; de deux actions simultanées sur le même
    /// tour, une seule est enregistrée.
    /// </summary>
    public int Version { get; set; }

    public ICollection<ActionDraftChampions> Actions { get; set; } = new List<ActionDraftChampions>();

    /// <summary>L'Équipe côté blue pour la game.</summary>
    public string EquipeBlue => Game.EquipeOn(Sides.Blue);

    public bool EstTerminee => Actions.Count >= OrdreTournament.ToursCount;

    /// <summary>Le tour à jouer, null quand la Draft de champions est terminée.</summary>
    public TourDraft? TourCourant => OrdreTournament.TourCourant(Actions.Count);

    /// <summary>L'Équipe qui joue le tour <paramref name="numero"/>.</summary>
    public string EquipeDuTour(int numero) => OrdreTournament.EquipeAttendue(numero, EquipeBlue);

    /// <summary>Le champion est déjà banni ou pick dans cette game.</summary>
    public bool AUtilise(string championId) => Actions.Any(a => a.ChampionId == championId);

    /// <summary>Joue le tour courant avec ce champion ; le tour suivant commence à <paramref name="maintenant"/>.</summary>
    public void Jouer(string championId, DateTime maintenant)
    {
        var tour = TourCourant ?? throw new InvalidOperationException("La Draft de champions est terminée.");
        Actions.Add(new ActionDraftChampions { Numero = tour.Numero, ChampionId = championId });
        TourCommenceLe = maintenant;
        Version++;
    }
}

/// <summary>Un tour joué de la Draft de champions : son numéro (1 à 20) et le champion banni ou pick.</summary>
public class ActionDraftChampions
{
    public int Id { get; set; }
    public int DraftChampionsId { get; set; }
    public int Numero { get; set; }

    /// <summary>L'identifiant Data Dragon du champion (ex. "LeeSin").</summary>
    public string ChampionId { get; set; } = "";
}
