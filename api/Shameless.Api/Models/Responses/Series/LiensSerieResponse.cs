using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Series;

/// <summary>
/// Les liens d'accès d'une Série, pour l'organisateur : un Lien capitaine par Équipe et le Lien
/// spectateur, disponible dès la création de la Série.
/// </summary>
public sealed record LiensSerieResponse(
    int SeriesId,
    string LienCapitaineA,
    string LienCapitaineB,
    string LienSpectateur,
    DateTime? CapitaineARejointLe,
    DateTime? CapitaineBRejointLe)
{
    public static LiensSerieResponse From(BestOfSeries series) => new(
        SeriesId: series.Id,
        LienCapitaineA: series.LienCapitaine(Equipes.A).Secret,
        LienCapitaineB: series.LienCapitaine(Equipes.B).Secret,
        LienSpectateur: series.LienSpectateur.Secret,
        CapitaineARejointLe: series.LienCapitaine(Equipes.A).RejointLe,
        CapitaineBRejointLe: series.LienCapitaine(Equipes.B).RejointLe);
}

/// <summary>Ce que donne un lien : sa Série, sa portée et, pour un Lien capitaine, son Équipe ("A" | "B").</summary>
public sealed record AccesParLienResponse(int SeriesId, PorteeLien Portee, string? Equipe)
{
    public static AccesParLienResponse From(AccesParLien acces) => new(acces.SeriesId, acces.Portee, acces.Equipe);
}
