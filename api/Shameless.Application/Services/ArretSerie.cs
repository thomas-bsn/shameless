namespace Shameless.Application.Services;

/// <summary>Ce que devient une Série qu'on arrête (ADR 0009).</summary>
public enum ArretSerie
{
    /// <summary>Aucune game terminée : la Série est effacée avec ses games.</summary>
    Annulee,

    /// <summary>Au moins une game terminée : la Série se termine sans vainqueur, ses games terminées restent.</summary>
    Interrompue
}
