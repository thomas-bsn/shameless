namespace Shameless.Application.ChampionDraft;

/// <summary>Qui joue un tour de la Draft de champions, et à quelle condition.</summary>
public abstract record JoueurDraft
{
    private JoueurDraft() { }

    /// <summary>Le capitaine de l'Équipe, par son Lien capitaine : il joue les tours de son Équipe.</summary>
    public sealed record Capitaine(string Equipe) : JoueurDraft;

    /// <summary>L'organisateur connecté : il joue à la place de l'Équipe attendue, une fois l'échéance du tour atteinte.</summary>
    public sealed record Organisateur : JoueurDraft;
}
