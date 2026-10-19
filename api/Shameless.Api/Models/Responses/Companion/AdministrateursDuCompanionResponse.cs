namespace Shameless.Api.Models.Responses.Companion;

/// <summary>
/// À qui donner un Fichier de Rapport quand l'envoi échoue (ADR 0020) : les pseudos Discord du
/// Superadministrateur et des Administrateurs, et rien d'autre.
/// </summary>
public sealed record AdministrateursDuCompanionResponse(List<string> Superadministrateurs, List<string> Administrateurs);
