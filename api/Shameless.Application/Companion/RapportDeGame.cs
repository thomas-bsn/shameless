using Shameless.Application.Entities;

namespace Shameless.Application.Companion;

/// <summary>
/// Le rapport de fin de game envoyé par le Companion (ADR 0004) : l'identifiant Riot stable de la
/// partie, ses données Riot brutes et, pour chacun des dix joueurs détectés, son PUUID, son Side, son
/// champion joué, sa victoire et ses stats brutes. Il ne désigne pas de Série : elle vient du code
/// Companion.
/// </summary>
/// <param name="Donnees">Les données Riot brutes de la partie, en JSON ; null si absentes (rapport invalide).</param>
public sealed record RapportDeGame(string RiotMatchId, string? Donnees, IReadOnlyList<JoueurDuRapport> Joueurs)
{
    public const int LongueurMaxRiotMatchId = 100;
    public const int LongueurMaxChampionId = 50;

    /// <summary>Pourquoi ce rapport est mal formé, ou null s'il est lisible.</summary>
    public string? Invalidite()
    {
        if (string.IsNullOrWhiteSpace(RiotMatchId) || RiotMatchId.Length > LongueurMaxRiotMatchId)
            return $"L'identifiant Riot de la partie est obligatoire ({LongueurMaxRiotMatchId} caractères au plus).";
        if (Donnees == null)
            return "Le rapport porte les données Riot de la partie (objet JSON).";
        if (Joueurs.Count != Equipes.PlayersPerTeam * 2)
            return "Un rapport décrit les dix joueurs de la partie.";
        if (Joueurs.Any(j => string.IsNullOrWhiteSpace(j.Puuid)))
            return "Chaque joueur du rapport a un PUUID.";
        if (Joueurs.Select(j => j.Puuid).Distinct().Count() != Joueurs.Count)
            return "Les dix joueurs d'un rapport sont tous distincts.";
        if (Joueurs.Any(j => j.Side is not (Sides.Blue or Sides.Red)))
            return "Le Side d'un joueur est « blue » ou « red ».";
        if (Joueurs.Count(j => j.Side == Sides.Blue) != Equipes.PlayersPerTeam)
            return "Chaque Side compte exactement cinq joueurs.";
        if (Joueurs.Any(j => string.IsNullOrWhiteSpace(j.ChampionId) || j.ChampionId.Length > LongueurMaxChampionId))
            return "Chaque joueur du rapport a joué un champion.";
        if (Joueurs.Any(j => j.Stats == null))
            return "Le rapport porte les stats de chaque joueur (objet JSON).";
        if (Joueurs.GroupBy(j => j.Side).Any(side => side.Select(j => j.Victoire).Distinct().Count() != 1)
            || Joueurs.Count(j => j.Victoire) != Equipes.PlayersPerTeam)
            return "Les cinq joueurs d'un Side gagnent ensemble, contre les cinq de l'autre.";
        return null;
    }

    /// <summary>Le Side vainqueur ; le rapport est supposé lisible (<see cref="Invalidite"/>).</summary>
    public string SideGagnant => Joueurs.First(j => j.Victoire).Side;

    /// <summary>
    /// L'Équipe gagnante dans la game : celle de la plupart des participants détectés sur le Side
    /// gagnant, même si les Sides de la game sont inversés ; à défaut, l'Équipe qui joue ce Side.
    /// </summary>
    public string EquipeGagnanteDans(CustomGame game)
    {
        var gagnants = Joueurs.Where(j => j.Victoire).Select(j => j.Puuid).ToHashSet();
        var parEquipe = game.Participants
            .Where(p => gagnants.Contains(p.Player.PUUID))
            .GroupBy(p => game.EquipeOn(p.Side))
            .Select(g => (Equipe: g.Key, Nombre: g.Count()))
            .OrderByDescending(g => g.Nombre)
            .ToList();
        return parEquipe.Count == 0 || (parEquipe.Count == 2 && parEquipe[0].Nombre == parEquipe[1].Nombre)
            ? game.EquipeOn(SideGagnant)
            : parEquipe[0].Equipe;
    }

    /// <summary>
    /// Vrai quand le rapport corrige le Side de la game : tous les participants détectés sont vus sur
    /// l'autre Side que prévu, c'est-à-dire que les deux Équipes ont simplement échangé leur camp.
    /// Le Rapport fait foi sur le Side, ce n'est donc pas un écart.
    /// </summary>
    public bool InverseLesSidesDe(CustomGame game)
    {
        var detectes = Joueurs.ToDictionary(j => j.Puuid);
        var presents = game.Participants.Where(p => detectes.ContainsKey(p.Player.PUUID)).ToList();
        return presents.Count > 0 && presents.All(p => detectes[p.Player.PUUID].Side != p.Side);
    }

    /// <summary>
    /// Ce qui sépare les joueurs détectés de la composition de la game (ses participants,
    /// Remplacements compris) : participants absents du rapport, joueurs détectés qui n'en font pas
    /// partie, participants détectés sur l'autre Side. Deux Équipes qui ont échangé leur Side ne
    /// sont pas un écart (<see cref="InverseLesSidesDe"/>).
    /// </summary>
    public EcartDeComposition EcartAvec(CustomGame game)
    {
        var detectes = Joueurs.ToDictionary(j => j.Puuid);
        var attendus = game.Participants.Select(p => p.Player.PUUID).ToHashSet();
        var inverse = InverseLesSidesDe(game);
        return new EcartDeComposition(
            Absents: game.Participants.Where(p => !detectes.ContainsKey(p.Player.PUUID)).Select(p => p.PlayerId).ToList(),
            Inattendus: Joueurs.Where(j => !attendus.Contains(j.Puuid)).Select(j => j.Puuid).ToList(),
            MauvaisSide: inverse
                ? []
                : game.Participants
                    .Where(p => detectes.TryGetValue(p.Player.PUUID, out var j) && j.Side != p.Side)
                    .Select(p => p.PlayerId)
                    .ToList());
    }
}

/// <param name="Stats">Les stats brutes du joueur pour la partie, en JSON ; null si absentes (rapport invalide).</param>
/// <param name="RiotId">Le RiotId (Nom#TAG) d'un joueur inconnu de l'Instance, retrouvé par Shameless à la mise en revue du rapport ; null sinon.</param>
public sealed record JoueurDuRapport(string Puuid, string Side, string ChampionId, bool Victoire, string? Stats, string? RiotId = null);

/// <summary>
/// L'écart entre les joueurs détectés par un rapport et la composition d'une game. Vide, le rapport
/// est conforme.
/// </summary>
/// <param name="Absents">Les participants de la game (id joueur) absents du rapport.</param>
/// <param name="Inattendus">Les PUUID détectés qui ne sont pas participants de la game.</param>
/// <param name="MauvaisSide">Les participants (id joueur) détectés sur l'autre Side que prévu.</param>
public sealed record EcartDeComposition(
    IReadOnlyList<int> Absents,
    IReadOnlyList<string> Inattendus,
    IReadOnlyList<int> MauvaisSide)
{
    public bool EstVide => Absents.Count == 0 && Inattendus.Count == 0 && MauvaisSide.Count == 0;
}
