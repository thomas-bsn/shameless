using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Series;

/// <summary>
/// Les liens d'accès d'une Série, pour l'organisateur : un Lien capitaine par Équipe et le Lien
/// spectateur, disponible dès la création de la Série. <c>EchecDmA</c> et <c>EchecDmB</c> signalent un DM
/// que le bot n'a pas pu envoyer au Capitaine de l'Équipe : le lien est alors à copier à la main.
/// </summary>
public sealed record LiensSerieResponse(
    int SeriesId,
    string LienCapitaineA,
    string LienCapitaineB,
    string LienSpectateur,
    EchecDmResponse? EchecDmA,
    EchecDmResponse? EchecDmB,
    DateTime? CapitaineARejointLe,
    DateTime? CapitaineBRejointLe,
    DateTime? CapitaineAPretLe,
    DateTime? CapitaineBPretLe)
{
    public static LiensSerieResponse From(BestOfSeries series) => new(
        SeriesId: series.Id,
        LienCapitaineA: series.LienCapitaine(Equipes.A).Secret,
        LienCapitaineB: series.LienCapitaine(Equipes.B).Secret,
        LienSpectateur: series.LienSpectateur.Secret,
        EchecDmA: EchecDmResponse.From(series.LienEnEchecDm(Equipes.A)),
        EchecDmB: EchecDmResponse.From(series.LienEnEchecDm(Equipes.B)),
        CapitaineARejointLe: series.LienCapitaine(Equipes.A).RejointLe,
        CapitaineBRejointLe: series.LienCapitaine(Equipes.B).RejointLe,
        CapitaineAPretLe: series.LienCapitaine(Equipes.A).PretLe,
        CapitaineBPretLe: series.LienCapitaine(Equipes.B).PretLe);
}

/// <summary>Un DM de Lien capitaine non parti : <c>Raison</c> vaut <c>compte_non_lie</c>, <c>dm_ferme</c> ou <c>autre</c>.</summary>
public sealed record EchecDmResponse(string Raison, DateTime SignaleLe)
{
    public static EchecDmResponse? From(LienAcces? lien) =>
        lien is { EchecDmRaison: { } raison, EchecDmLe: { } le } ? new EchecDmResponse(raison, le) : null;
}

/// <summary>
/// Ce que donne un lien : sa Série, sa portée et, pour un Lien capitaine, son Équipe ("A" | "B"), avec
/// l'état du « prêt » : celui du Capitaine (<c>Pret</c>) et celui de son adversaire (<c>AdversairePret</c>),
/// faux pour un Lien spectateur. <c>PretPossible</c> : la Série est en cours et aucune Draft de champions
/// n'est ouverte sur une game en attente, donc le Capitaine peut se déclarer prêt pour la prochaine game.
/// </summary>
public sealed record AccesParLienResponse(int SeriesId, PorteeLien Portee, string? Equipe, bool Pret, bool AdversairePret, bool PretPossible)
{
    public static AccesParLienResponse From(AccesParLien acces, EtatDuPret pret) =>
        new(acces.SeriesId, acces.Portee, acces.Equipe, pret.Pret, pret.AdversairePret, pret.PretPossible);
}
