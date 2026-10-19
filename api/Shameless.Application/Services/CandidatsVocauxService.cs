using Shameless.Application.Interfaces;
using Shameless.Application.Discord;

namespace Shameless.Application.Services;

/// <summary>
/// Les candidats vocaux du Picker : qui est en vocal selon le bot Discord, résolu en Joueurs liés
/// (Utilisateur Discord lié à un Joueur). Le bot ne donne que des IDs ; les noms viennent de Shameless.
/// </summary>
public sealed class CandidatsVocauxService
{
    private readonly IBotDiscord _bot;
    private readonly IPlayerRepository _players;

    public CandidatsVocauxService(IBotDiscord bot, IPlayerRepository players)
    {
        _bot = bot;
        _players = players;
    }

    /// <summary>
    /// Les Joueurs liés présents en vocal, dans l'ordre des IDs Discord, et le nombre de membres en
    /// vocal sans Joueur lié. Lève <see cref="BotIndisponibleException"/> si le bot est indisponible.
    /// </summary>
    public async Task<CandidatsVocaux> GetAsync(CancellationToken cancellationToken = default)
    {
        var vocal = await _bot.GetCandidatsVocauxAsync(cancellationToken);
        var lies = vocal.MemberIds.Count == 0
            ? []
            : await _players.GetByDiscordIdsAsync(vocal.MemberIds);
        // Chaque Compte Discord lié compte comme son Joueur (ADR 0018) ; un Joueur n'est proposé qu'une fois,
        // avec le premier de ses comptes présents en vocal.
        var parDiscordId = lies
            .SelectMany(p => p.Users.Select(u => (u.DiscordId, Player: p)))
            .ToDictionary(x => x.DiscordId, x => x.Player);

        var joueurs = new List<CandidatVocal>();
        foreach (var id in vocal.MemberIds)
        {
            if (!parDiscordId.TryGetValue(id, out var joueur) || joueurs.Any(j => j.PlayerId == joueur.Id)) continue;
            joueurs.Add(new CandidatVocal(joueur.Id, joueur.Prenom, DiscordId.Texte(id)));
        }
        return new CandidatsVocaux(vocal.ChannelId, joueurs, vocal.MemberIds.Count(id => !parDiscordId.ContainsKey(id)));
    }
}

/// <summary>Les candidats vocaux du Picker.</summary>
/// <param name="ChannelId">Le salon vocal retenu par le bot, null si personne n'est en vocal.</param>
/// <param name="Joueurs">Les Joueurs liés présents, à ajouter au Picker.</param>
/// <param name="MembresNonLies">Les membres en vocal sans Joueur lié (Spectateurs ou comptes non liés).</param>
public sealed record CandidatsVocaux(string? ChannelId, List<CandidatVocal> Joueurs, int MembresNonLies);

/// <summary>Un Joueur lié présent en vocal, avec l'ID Discord (en texte) de son compte.</summary>
public sealed record CandidatVocal(int PlayerId, string Prenom, string DiscordId);
