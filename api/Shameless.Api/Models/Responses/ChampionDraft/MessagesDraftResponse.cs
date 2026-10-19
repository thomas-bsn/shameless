using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.ChampionDraft;

/// <summary>Un message du chat de la draft. <paramref name="Equipe"/> : l'Équipe du Capitaine ; null pour un spectateur.</summary>
public sealed record MessageDraftResponse(int Id, string Auteur, string? AvatarUrl, string? Equipe, string Contenu, DateTime EnvoyeLe)
{
    public static MessageDraftResponse From(MessageDraftChampions m) =>
        new(m.Id, m.Auteur, m.AvatarUrl, m.Equipe, m.Contenu, DateTime.SpecifyKind(m.EnvoyeLe, DateTimeKind.Utc));
}

/// <summary>Les messages du chat de la draft en cours, du plus ancien au plus récent ; vide une fois la draft terminée.</summary>
public sealed record MessagesDraftResponse(List<MessageDraftResponse> Messages);
