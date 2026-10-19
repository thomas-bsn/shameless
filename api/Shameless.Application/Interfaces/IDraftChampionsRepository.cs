using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface IDraftChampionsRepository
{
    /// <summary>Ajoute la Draft de champions ; effectif au prochain enregistrement.</summary>
    void Add(DraftChampions draft);

    /// <summary>Supprime la Draft de champions et ses actions (Reset accepté) ; effectif au prochain enregistrement.</summary>
    void Remove(DraftChampions draft);

    /// <summary>La Draft de champions de la game, avec ses actions, sa game et la Série de la game (composition comprise).</summary>
    Task<DraftChampions?> GetByGameAsync(int gameId);

    /// <summary>La Draft de champions de la dernière game de la Série qui en a une, chargée comme <see cref="GetByGameAsync"/>.</summary>
    Task<DraftChampions?> GetDerniereOfSeriesAsync(int seriesId);

    /// <summary>
    /// Enregistre les changements en une transaction. Renvoie false si une commande simultanée a modifié
    /// la même Draft de champions entre-temps : rien n'est enregistré.
    /// </summary>
    Task<bool> TrySaveChangesAsync();

    /// <summary>Les messages du chat de la Draft de champions, du plus ancien au plus récent.</summary>
    Task<List<MessageDraftChampions>> GetMessagesAsync(int draftId);

    /// <summary>Combien de messages cet émetteur a écrits depuis <paramref name="depuis"/>.</summary>
    Task<int> CountMessagesDepuisAsync(int draftId, string emetteur, DateTime depuis);

    /// <summary>Le pseudo déjà attribué à cet émetteur dans ce chat, null s'il n'a rien écrit.</summary>
    Task<string?> GetAuteurAsync(int draftId, string emetteur);

    /// <summary>Le plus grand numéro de « Spectateur N » déjà attribué dans la draft (0 si aucun).</summary>
    Task<int> DernierNumeroSpectateurAsync(int draftId);

    /// <summary>Ajoute le message et l'enregistre ; false (message détaché) si la base le refuse, par exemple un numéro de spectateur déjà pris.</summary>
    Task<bool> TryAddMessageAsync(MessageDraftChampions message);

    /// <summary>
    /// Ajoute le message d'un spectateur sans connexion : reprend son pseudo s'il en a déjà un, sinon lui donne le
    /// numéro suivant. Les arrivées simultanées dans une même draft sont mises en file, jamais refusées pour un numéro.
    /// </summary>
    Task<bool> TryAddMessageSpectateurAsync(MessageDraftChampions message);
}
