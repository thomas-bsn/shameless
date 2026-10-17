using Shameless.Application.Entities;
using Shameless.Application.Votes;
using Shameless.Application.Discord;

namespace Shameless.Application.Pronostics;

/// <summary>
/// Les pronostics des Spectateurs sur une Game, chaque choix évalué séparément dès que son résultat est
/// disponible : l'Équipe gagnante à la fin de la Game, le MVP et le DOFD à la clôture des votes. Ni
/// points ni classement : les pronostics sont listés par nom.
/// </summary>
/// <param name="GameNumber">Le numéro de la Game dans sa Série.</param>
/// <param name="VainqueurConnu">La Game est terminée : la justesse de l'Équipe gagnante est connue.</param>
/// <param name="VotesClos">Les votes sont clos : la justesse du MVP et du DOFD est connue.</param>
public sealed record PronosticsDeLaGame(
    int GameId,
    int GameNumber,
    bool VainqueurConnu,
    bool VotesClos,
    List<PronosticEvalue> Pronostics)
{
    /// <summary>
    /// Évalue les pronostics d'une Game (chargée avec sa Série et ses games) d'après ses votes. Un
    /// pronostic déposé après le lancement de la Game (<c>CreatedAt</c>) est ignoré : il arrivait trop
    /// tard. Un MVP ou un DOFD est juste si le Joueur pronostiqué est l'un des gagnants, égalités
    /// comprises ; faux si personne n'a été élu.
    /// </summary>
    public static PronosticsDeLaGame Evaluer(CustomGame game, IEnumerable<Pronostic> pronostics, ResultatsDesVotes votes)
    {
        var vainqueurConnu = game.Status == GameStatuses.Completed && game.WinningTeam != null;
        var votesClos = votes.Statut == FenetreDeVote.Clos;

        JoueurPronostique? Joueur(int? playerId, Player? player, ResultatDuTitre titre) =>
            playerId is { } id
                ? new JoueurPronostique(id, player?.Prenom ?? "", votesClos ? titre.Gagnants.Contains(id) : null)
                : null;

        var evalues = pronostics
            .Where(p => p.PronostiqueLe <= game.CreatedAt)
            .Select(p => new PronosticEvalue(
                DiscordId.Texte(p.PronostiqueurDiscordId),
                p.PronostiqueurNom,
                p.EquipeGagnante is { } equipe
                    ? new EquipePronostiquee(equipe, vainqueurConnu ? equipe == game.WinningTeam : null)
                    : null,
                Joueur(p.MvpPlayerId, p.Mvp, votes.Mvp),
                Joueur(p.DofdPlayerId, p.Dofd, votes.Dofd)))
            .OrderBy(p => p.PronostiqueurNom ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.PronostiqueurDiscordId, StringComparer.Ordinal)
            .ToList();

        return new PronosticsDeLaGame(game.Id, game.NumeroDansLaSerie, vainqueurConnu, votesClos, evalues);
    }
}

/// <summary>Le pronostic évalué d'un Spectateur ; un choix null n'a pas été fait.</summary>
/// <param name="PronostiqueurDiscordId">L'ID Discord (texte) du Spectateur.</param>
/// <param name="PronostiqueurNom">Son nom affiché sur Discord au moment du pronostic, s'il est connu.</param>
public sealed record PronosticEvalue(
    string PronostiqueurDiscordId,
    string? PronostiqueurNom,
    EquipePronostiquee? EquipeGagnante,
    JoueurPronostique? Mvp,
    JoueurPronostique? Dofd);

/// <summary>L'Équipe pronostiquée gagnante.</summary>
/// <param name="Correct">Juste ou non, null tant que la Game n'est pas terminée.</param>
public sealed record EquipePronostiquee(string Equipe, bool? Correct);

/// <summary>Le Joueur pronostiqué MVP ou DOFD.</summary>
/// <param name="Correct">Juste ou non, null tant que les votes ne sont pas clos.</param>
public sealed record JoueurPronostique(int PlayerId, string Prenom, bool? Correct);
