using Shameless.Application.Entities;
using Shameless.Application.Services;

namespace Shameless.Api.Models.Responses.Companion;

/// <summary>Le Joueur d'un Companion connecté, retrouvé par PUUID.</summary>
public sealed record JoueurDuCompanionResponse(int Id, string Prenom, string RiotId);

/// <summary>
/// Un Companion connecté, pour l'Administration : sa Série, son Joueur (null si le PUUID est inconnu de
/// l'Instance) et « vu il y a X s ».
/// </summary>
public sealed record CompanionConnecteResponse(int SeriesId, JoueurDuCompanionResponse? Joueur, int VuIlYASecondes)
{
    public static CompanionConnecteResponse From(CompanionConnecte connecte, Application.Entities.Player? joueur) => new(
        connecte.SeriesId,
        joueur == null ? null : new JoueurDuCompanionResponse(joueur.Id, joueur.Prenom, joueur.RiotId),
        (int)connecte.VuIlYA.TotalSeconds);
}
