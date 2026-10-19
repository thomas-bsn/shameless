using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>Qui écrit dans le chat quand le lien est un Lien spectateur.</summary>
public abstract record AuteurChat
{
    /// <summary>Un spectateur connecté avec Discord.</summary>
    public sealed record Discord(long DiscordId, string Nom, string? AvatarUrl) : AuteurChat;

    /// <summary>Un spectateur sans connexion, reconnu par un jeton que garde son navigateur.</summary>
    public sealed record Anonyme(string? Jeton) : AuteurChat;
}

/// <summary>
/// Le chat de la Draft de champions : lu et écrit par les liens de la Série (Capitaines et spectateurs).
/// Un message est limité en longueur et le débit de chaque émetteur est limité ; une fois la draft
/// terminée, le chat est fermé et ses messages ne sont plus affichés.
/// </summary>
public sealed class ChatDraftChampionsService
{
    private const int LongueurMaxJeton = 64;
    private const int EssaisMax = 5;

    private readonly IDraftChampionsRepository _drafts;
    private readonly TimeProvider _clock;

    public ChatDraftChampionsService(IDraftChampionsRepository drafts, TimeProvider clock)
    {
        _drafts = drafts;
        _clock = clock;
    }

    /// <summary>Les messages de la dernière draft de la Série ; aucun une fois la draft terminée.</summary>
    public async Task<CommandResult<List<MessageDraftChampions>>> LireAsync(AccesParLien acces)
    {
        var draft = await _drafts.GetDerniereOfSeriesAsync(acces.SeriesId);
        if (draft == null)
            return CommandResult<List<MessageDraftChampions>>.NotFound("Aucune Draft de champions n'est ouverte dans cette Série.");
        if (draft.EstTerminee)
            return CommandResult<List<MessageDraftChampions>>.Ok([]);

        return CommandResult<List<MessageDraftChampions>>.Ok(await _drafts.GetMessagesAsync(draft.Id));
    }

    /// <summary>Écrit un message ; <paramref name="auteur"/> ne sert qu'aux spectateurs (Lien spectateur).</summary>
    public async Task<CommandResult<MessageDraftChampions>> EcrireAsync(AccesParLien acces, string? contenu, AuteurChat auteur)
    {
        var texte = contenu?.Trim() ?? "";
        if (texte.Length == 0)
            return CommandResult<MessageDraftChampions>.Invalid("Le message est vide.");
        if (texte.Length > MessageDraftChampions.LongueurMax)
            return CommandResult<MessageDraftChampions>.Invalid(
                $"Le message est trop long : {MessageDraftChampions.LongueurMax} caractères au plus.");

        var draft = await _drafts.GetDerniereOfSeriesAsync(acces.SeriesId);
        if (draft == null)
            return CommandResult<MessageDraftChampions>.NotFound("Aucune Draft de champions n'est ouverte dans cette Série.");
        if (draft.EstTerminee)
            return CommandResult<MessageDraftChampions>.Conflict("La draft est terminée : le chat est fermé.");

        var identite = await IdentiteAsync(draft, acces, auteur);
        if (!identite.IsOk) return identite.As<MessageDraftChampions>();
        var (emetteur, nom, avatar, equipe) = identite.Value;

        var maintenant = _clock.MaintenantUtc();
        if (await _drafts.CountMessagesDepuisAsync(draft.Id, emetteur, maintenant - MessageDraftChampions.Fenetre)
            >= MessageDraftChampions.MessagesParFenetre)
            return CommandResult<MessageDraftChampions>.TooManyRequests(
                "Vous écrivez trop vite : patientez quelques secondes avant d'envoyer un autre message.");

        // Un nouveau spectateur sans connexion reçoit le numéro suivant ; si un autre arrive en même temps et
        // prend le même numéro, la base refuse et on recalcule (le pseudo d'un spectateur déjà connu est repris).
        for (var essai = 0; essai < EssaisMax; essai++)
        {
            var message = new MessageDraftChampions
            {
                DraftChampionsId = draft.Id,
                Emetteur = emetteur,
                Auteur = nom,
                AvatarUrl = avatar,
                Equipe = equipe,
                Contenu = texte,
                EnvoyeLe = maintenant
            };
            if (emetteur.StartsWith(MessageDraftChampions.PrefixeAnonyme))
            {
                var connu = await _drafts.GetAuteurAsync(draft.Id, emetteur);
                if (connu != null)
                    message.Auteur = connu;
                else
                {
                    message.NumeroSpectateur = await _drafts.DernierNumeroSpectateurAsync(draft.Id) + 1;
                    message.Auteur = $"Spectateur {message.NumeroSpectateur}";
                }
            }
            if (await _drafts.TryAddMessageAsync(message))
                return CommandResult<MessageDraftChampions>.Ok(message);
        }
        return CommandResult<MessageDraftChampions>.Conflict("Le chat est très sollicité : réessayez dans un instant.");
    }

    private async Task<CommandResult<(string Emetteur, string Nom, string? Avatar, string? Equipe)>> IdentiteAsync(
        DraftChampions draft, AccesParLien acces, AuteurChat auteur)
    {
        if (acces.Portee == PorteeLien.Capitaine)
        {
            var equipe = acces.Equipe!;
            var capitaineId = draft.Game.Series?.CapitaineOf(equipe);
            var joueur = draft.Game.Participants.FirstOrDefault(p => p.PlayerId == capitaineId)?.Player;
            return Ok(MessageDraftChampions.PrefixeCapitaine + equipe, joueur?.Prenom ?? $"Capitaine {equipe}", joueur?.User?.AvatarUrl, equipe);
        }

        switch (auteur)
        {
            case AuteurChat.Discord d:
                return Ok(MessageDraftChampions.PrefixeDiscord + d.DiscordId, d.Nom, d.AvatarUrl, null);
            case AuteurChat.Anonyme { Jeton: { Length: > 0 and <= LongueurMaxJeton } jeton }:
                // Le pseudo « Spectateur N » est attribué à l'enregistrement, dans EcrireAsync.
                return Ok(MessageDraftChampions.PrefixeAnonyme + jeton, "", null, null);
            default:
                return CommandResult<(string Emetteur, string Nom, string? Avatar, string? Equipe)>.Invalid(
                    "Identifiant de spectateur manquant ou trop long.");
        }

        static CommandResult<(string Emetteur, string Nom, string? Avatar, string? Equipe)> Ok(
            string emetteur, string nom, string? avatar, string? equipe) =>
            CommandResult<(string Emetteur, string Nom, string? Avatar, string? Equipe)>.Ok((emetteur, nom, avatar, equipe));
    }
}
