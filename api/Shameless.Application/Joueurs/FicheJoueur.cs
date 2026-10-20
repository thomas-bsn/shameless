using Shameless.Application.Entities;
using Shameless.Application.Picker;
using Shameless.Application.Stats;
using Shameless.Application.Votes;

namespace Shameless.Application.Joueurs;

/// <summary>
/// La fiche d'un Joueur : ses stats de customs, ses champions et rôles joués, ses notes de performance
/// et son historique de Games terminées, avec son compte Discord lié, son cost courant et ses rangs actuels.
/// </summary>
/// <param name="Discord">Le compte Discord lié, null si aucun.</param>
/// <param name="Cost">Le cost courant du Joueur.</param>
/// <param name="SoloQ">Le Rang SoloQ actuel, null si non classé.</param>
/// <param name="Flex">Le rang actuel en file Flex, null si non classé.</param>
/// <param name="Champions">Les champions joués, du plus joué au moins joué.</param>
/// <param name="Roles">Les rôles joués, du plus joué au moins joué.</param>
/// <param name="Games">Les Games terminées du Joueur, de la plus récente à la plus ancienne.</param>
/// <param name="Titres">Les titres de DOFD remportés par le Joueur (votes clos, égalités comprises).</param>
/// <param name="JoueAvec">Les coéquipiers du Joueur, du plus fréquent au moins fréquent.</param>
/// <param name="Resume">Le résumé de ses 30 dernières games.</param>
/// <param name="Liste">Ses games groupées et filtrées selon la demande ; <paramref name="Games"/> reste l'historique complet.</param>
public sealed record FicheJoueur(
    int PlayerId,
    string Prenom,
    string RiotId,
    CompteDiscordLie? Discord,
    List<CompteDiscordLie> ComptesDiscord,
    decimal Cost,
    RangActuel? SoloQ,
    RangActuel? Flex,
    StatsDeCustoms Stats,
    List<StatsParChampion> Champions,
    List<StatsParRole> Roles,
    List<GameDeLaFiche> Games,
    TitresDuJoueur Titres,
    List<CoequipierDeLaFiche> JoueAvec,
    ResumeDesDernieresGames Resume,
    List<GroupeDeGames> Liste)
{
    /// <summary>
    /// La fiche du Joueur à partir de ses Games. Seules les Games terminées comptent (y compris celles
    /// d'une Série interrompue). Les Games doivent être chargées avec leurs participants et leurs
    /// Joueurs (et <c>Series.Players</c> pour le rôle de l'Autorole), comme pour <see cref="NotesDeLaGame"/>.
    /// <paramref name="fuseau"/> : celui de l'Instance, pour le regroupement par jour (UTC sans lui).
    /// </summary>
    public static FicheJoueur Calculer(
        Player joueur, IEnumerable<CustomGame> games, IReadOnlyDictionary<int, ResultatsDesVotes>? votes = null,
        FiltreDeGames? filtre = null, TimeZoneInfo? fuseau = null)
    {
        filtre ??= new FiltreDeGames();
        votes ??= new Dictionary<int, ResultatsDesVotes>();
        var titres = TitresDuJoueur.Compter(votes.Values).GetValueOrDefault(joueur.Id, TitresDuJoueur.Aucun);
        var parties = games
            .Where(g => g.Status == GameStatuses.Completed)
            .Select(g => PartieDuJoueur.De(g, joueur.Id))
            .OfType<PartieDuJoueur>()
            .OrderByDescending(p => p.Game.PlayedAt)
            .ThenByDescending(p => p.Game.Id)
            .ToList();

        var lignes = parties.Select(p => (Partie: p, Ligne: p.EnLigne(votes.GetValueOrDefault(p.Game.Id)))).ToList();

        return new FicheJoueur(
            joueur.Id,
            joueur.Prenom,
            joueur.RiotId,
            joueur.User is { } user ? new CompteDiscordLie(user.DiscordId.ToString(), user.DiscordUsername, user.AvatarUrl, true, user.BannerUrl, user.BannerColor) : null,
            joueur.Users
                .OrderBy(u => u.PlayerLinkedAt).ThenBy(u => u.Id)
                .Select(u => new CompteDiscordLie(u.DiscordId.ToString(), u.DiscordUsername, u.AvatarUrl, u.Id == joueur.UserId))
                .ToList(),
            joueur.CurrentCost,
            RangActuel.De(joueur.RankTier, joueur.RankDivision, joueur.LP),
            RangActuel.De(joueur.FlexRankTier, joueur.FlexRankDivision, joueur.FlexLp),
            StatsDeCustoms.Calculer(parties),
            parties
                .Where(p => ValeursEffectives.Champion(p.Participant) is not null)
                .GroupBy(p => ValeursEffectives.Champion(p.Participant)!)
                .Select(g => new StatsParChampion(g.Key, StatsDeCustoms.Calculer(g.ToList())))
                .OrderByDescending(c => c.Stats.GamesJouees)
                .ThenBy(c => c.ChampionId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            parties
                .Where(p => p.Role is not null)
                .GroupBy(p => p.Role!.Value)
                .Select(g => new StatsParRole(g.Key, StatsDeCustoms.Calculer(g.ToList())))
                .OrderByDescending(r => r.Stats.GamesJouees)
                .ThenBy(r => r.Role)
                .ToList(),
            lignes.Select(l => l.Ligne).ToList(),
            titres,
            FicheEnrichie.JoueAvec(joueur.Id, parties),
            ResumeDesDernieresGames.Calculer(parties, lignes.Select(l => l.Ligne).ToList()),
            FicheEnrichie.Liste(lignes, filtre, fuseau ?? TimeZoneInfo.Utc));
    }
}

/// <summary>Le compte Discord lié au Joueur.</summary>
/// <param name="DiscordId">L'identifiant Discord, en texte (trop grand pour un nombre JavaScript).</param>
/// <summary>
/// Un Compte Discord lié au Joueur ; <paramref name="Principal"/> pour le plus ancien lien (ADR 0018).
/// La bannière et sa couleur d'accent ne sont renseignées que sur le compte principal de <see cref="FicheJoueur.Discord"/>.
/// </summary>
public sealed record CompteDiscordLie(
    string DiscordId, string? Username, string? AvatarUrl, bool Principal, string? BannerUrl = null, string? BannerColor = null);

/// <summary>Les stats d'un champion joué par le Joueur.</summary>
/// <param name="ChampionId">Le champion (id Data Dragon).</param>
public sealed record StatsParChampion(string ChampionId, StatsDeCustoms Stats);

/// <summary>Les stats d'un rôle joué par le Joueur (position de la partie, sinon Autorole, sinon rôle principal).</summary>
public sealed record StatsParRole(Role Role, StatsDeCustoms Stats);

/// <summary>Une Game terminée du Joueur, avec sa note de performance.</summary>
/// <param name="Equipe">L'Équipe du Joueur : « A » ou « B ».</param>
/// <param name="Victoire">true si son Équipe a gagné.</param>
/// <param name="DureeSecondes">La durée de la game, null sans données du Companion.</param>
/// <param name="ChampionId">Le champion joué, null sans rapport du Companion pour le Joueur.</param>
/// <param name="Role">Le rôle joué, null si inconnu.</param>
/// <param name="Stats">Ses stats, null sans rapport du Companion pour le Joueur.</param>
/// <param name="Performance">Sa note de performance, null sans stats suffisantes.</param>
/// <param name="Dofd">true si le Joueur a le titre de DOFD de la Game : votes clos, égalités comprises.</param>
/// <param name="Place">Sa place de 1 à 10 dans la game d'après sa note, null sans note.</param>
/// <param name="Badge">Son badge de note (Meilleure perf, ACE), null sinon.</param>
/// <param name="ParticipationAuxKills">Ses kills et assists / les kills de son Équipe, en pourcentage (une décimale), null sans ces données.</param>
/// <param name="AdversaireChampionId">Le champion adverse du même poste (id Data Dragon), null sans poste connu ou sans adversaire unique.</param>
public sealed record GameDeLaFiche(
    int GameId,
    int? SeriesId,
    DateTime PlayedAt,
    string Equipe,
    bool Victoire,
    int? DureeSecondes,
    string? ChampionId,
    Role? Role,
    StatsDuParticipant? Stats,
    NoteDePerformance? Performance,
    bool Dofd = false,
    int? Place = null,
    BadgeDeGame? Badge = null,
    double? ParticipationAuxKills = null,
    string? AdversaireChampionId = null);
