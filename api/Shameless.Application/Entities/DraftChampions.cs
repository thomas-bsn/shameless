using Shameless.Application.ChampionDraft;
using Shameless.Application.Picker;

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

    /// <summary>Le délai de chaque action de capitaine ; passé l'échéance, l'organisateur peut jouer à sa place.</summary>
    public static readonly TimeSpan DelaiParTour = TimeSpan.FromSeconds(60);

    /// <summary>L'échéance du tour courant : <see cref="DelaiParTour"/> après son début.</summary>
    public DateTime Echeance => TourCommenceLe + DelaiParTour;

    /// <summary>L'échéance du tour courant est atteinte à <paramref name="maintenant"/>.</summary>
    public bool EcheanceAtteinte(DateTime maintenant) => maintenant >= Echeance;

    /// <summary>Durée maximale d'une pause : passé ce délai, la draft reprend toute seule.</summary>
    public static readonly TimeSpan DureeMaxPause = TimeSpan.FromMinutes(5);

    /// <summary>Délai de réponse à une demande de reprise anticipée : passé ce délai, elle tombe.</summary>
    public static readonly TimeSpan DelaiDemandeReprise = TimeSpan.FromSeconds(60);

    /// <summary>Quand la pause en cours a commencé ; null hors pause.</summary>
    public DateTime? PauseCommenceLe { get; set; }

    /// <summary>L'Équipe dont le Capitaine a mis la draft en pause ; null hors pause.</summary>
    public string? PauseParEquipe { get; set; }

    /// <summary>Le temps qui restait au tour courant quand la pause a commencé ; null hors pause.</summary>
    public TimeSpan? TempsRestantAvantPause { get; set; }

    /// <summary>Chaque Capitaine a droit à une seule pause par draft.</summary>
    public bool PauseUtiliseeA { get; set; }
    public bool PauseUtiliseeB { get; set; }

    /// <summary>L'Équipe dont le Capitaine demande la reprise anticipée ; null sans demande en cours.</summary>
    public string? RepriseDemandeeParEquipe { get; set; }

    public DateTime? RepriseDemandeeLe { get; set; }

    public bool EnPause => PauseCommenceLe != null;

    public bool PauseDisponible(string equipe) => !(equipe == Equipes.A ? PauseUtiliseeA : PauseUtiliseeB);

    /// <summary>Quand la pause en cours finit toute seule ; null hors pause.</summary>
    public DateTime? PauseFinLe => PauseCommenceLe + DureeMaxPause;

    /// <summary>Quand la demande de reprise en cours tombe ; null sans demande.</summary>
    public DateTime? RepriseExpireLe => RepriseDemandeeLe + DelaiDemandeReprise;

    /// <summary>
    /// Met l'état de la pause à <paramref name="maintenant"/> : la demande de reprise sans réponse
    /// tombe, et la pause de 5 minutes reprend la draft. Vrai si l'état a changé.
    /// </summary>
    public bool Actualiser(DateTime maintenant)
    {
        var change = false;
        if (RepriseDemandeeLe != null && maintenant >= RepriseExpireLe)
        {
            EffacerDemandeReprise();
            change = true;
        }
        if (EnPause && maintenant >= PauseFinLe)
        {
            Reprendre(PauseFinLe!.Value);
            change = true;
        }
        if (change) Version++;
        return change;
    }

    /// <summary>Met la draft en pause : le temps restant du tour est gelé. Une pause par Capitaine.</summary>
    public void MettreEnPause(string equipe, DateTime maintenant)
    {
        PauseCommenceLe = maintenant;
        PauseParEquipe = equipe;
        TempsRestantAvantPause = Echeance > maintenant ? Echeance - maintenant : TimeSpan.Zero;
        if (equipe == Equipes.A) PauseUtiliseeA = true; else PauseUtiliseeB = true;
        Version++;
    }

    public void DemanderReprise(string equipe, DateTime maintenant)
    {
        RepriseDemandeeParEquipe = equipe;
        RepriseDemandeeLe = maintenant;
        Version++;
    }

    public void RefuserReprise()
    {
        EffacerDemandeReprise();
        Version++;
    }

    public void ReprendreAnticipe(DateTime maintenant)
    {
        Reprendre(maintenant);
        Version++;
    }

    /// <summary>Rétablit le tour avec le temps restant gelé, à partir de <paramref name="quand"/>.</summary>
    private void Reprendre(DateTime quand)
    {
        TourCommenceLe = quand + TempsRestantAvantPause!.Value - DelaiParTour;
        PauseCommenceLe = null;
        PauseParEquipe = null;
        TempsRestantAvantPause = null;
        EffacerDemandeReprise();
    }

    private void EffacerDemandeReprise()
    {
        RepriseDemandeeParEquipe = null;
        RepriseDemandeeLe = null;
    }

    /// <summary>
    /// Jeton de concurrence : chaque action l'incrémente ; de deux actions simultanées sur le même
    /// tour, une seule est enregistrée.
    /// </summary>
    public int Version { get; set; }

    public ICollection<ActionDraftChampions> Actions { get; set; } = new List<ActionDraftChampions>();

    /// <summary>L'Équipe côté blue pour la game.</summary>
    public string EquipeBlue => Game.EquipeOn(Sides.Blue);

    /// <summary>
    /// La Draft de champions est terminée : ses vingt tours sont joués, ou le résultat de sa game est
    /// saisi avant (plus aucune action possible).
    /// </summary>
    public bool EstTerminee => Actions.Count >= OrdreTournament.ToursCount || Game.Status == GameStatuses.Completed;

    /// <summary>Le tour à jouer, null quand la Draft de champions est terminée.</summary>
    public TourDraft? TourCourant => EstTerminee ? null : OrdreTournament.TourCourant(Actions.Count);

    /// <summary>L'Équipe qui joue le tour <paramref name="numero"/>.</summary>
    public string EquipeDuTour(int numero) => OrdreTournament.EquipeAttendue(numero, EquipeBlue);

    /// <summary>
    /// Le rôle de l'Autorole du joueur dans la Série, révélé une fois la Draft de champions terminée ;
    /// null avant, ou sans Autorole.
    /// </summary>
    public Role? RoleRevele(int playerId) => EstTerminee ? Game.Series?.RoleOf(playerId) : null;

    /// <summary>
    /// Fearless : les champions réellement joués dans les games précédentes de la Série, d'après leurs
    /// rapports, sans doublon, dans l'ordre des games. Ils restent bannissables et pickables : l'app
    /// les montre, elle n'empêche rien. Null quand la Série n'est pas Fearless.
    /// </summary>
    public IReadOnlyList<string>? ChampionsDejaJoues => Game.Series is { Fearless: true } series
        ? series.Games
            .Where(g => g.Id < GameId && g.Status == GameStatuses.Completed)
            .OrderBy(g => g.Id)
            .SelectMany(g => g.Participants.OrderBy(p => p.Id))
            .Select(p => p.ChampionId)
            .OfType<string>()
            .Distinct()
            .ToList()
        : null;

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
