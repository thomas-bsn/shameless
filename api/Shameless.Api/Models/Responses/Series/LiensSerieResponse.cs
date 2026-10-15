using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Series;

/// <summary>
/// Les liens d'accès d'une Série, pour l'organisateur : un Lien capitaine par Équipe et le Lien
/// spectateur, null tant que l'organisateur n'a pas indiqué avoir envoyé les Liens capitaine.
/// </summary>
public sealed record LiensSerieResponse(
    int SeriesId,
    string LienCapitaineA,
    string LienCapitaineB,
    bool LiensCapitaineEnvoyes,
    string? LienSpectateur)
{
    public static LiensSerieResponse From(BestOfSeries series) => new(
        SeriesId: series.Id,
        LienCapitaineA: series.LienCapitaine(Equipes.A).Secret,
        LienCapitaineB: series.LienCapitaine(Equipes.B).Secret,
        LiensCapitaineEnvoyes: series.LiensCapitaineEnvoyes,
        LienSpectateur: series.LiensCapitaineEnvoyes ? series.LienSpectateur.Secret : null);
}

/// <summary>Ce que donne un lien : sa Série, sa portée et, pour un Lien capitaine, son Équipe ("A" | "B").</summary>
public sealed record AccesParLienResponse(int SeriesId, PorteeLien Portee, string? Equipe)
{
    public static AccesParLienResponse From(AccesParLien acces) => new(acces.SeriesId, acces.Portee, acces.Equipe);
}
