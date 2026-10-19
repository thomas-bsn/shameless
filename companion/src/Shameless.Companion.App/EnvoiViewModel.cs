using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Shameless.Companion.Core;

namespace Shameless.Companion.App;

/// <summary>
/// Ce que la fenêtre montre de l'envoi des Rapports : le dernier résultat, et la procédure d'échec (ADR 0020) avec
/// les pseudos à qui remettre le Fichier de Rapport. Isolé de <see cref="FenetreViewModel"/> : l'encart a son propre contexte.
/// </summary>
public sealed class EnvoiViewModel : INotifyPropertyChanged
{
    private readonly EnvoiRapports _envoi;
    private readonly CancellationToken _arret;
    private readonly Action<string> _reveler;
    private string _dernierResultat = "";
    private bool _echecVisible;
    private string _cheminFichier = "";
    private string _ligneSuperadministrateur = "";
    private string _ligneAdministrateur = "";

    public EnvoiViewModel(EnvoiRapports envoi, CancellationToken arret, Action<string> reveler)
    {
        _envoi = envoi;
        _arret = arret;
        _reveler = reveler;
        AfficherPseudos(envoi.Pseudos);
        // Les événements arrivent d'un fil d'arrière-plan : on ne touche la fenêtre que depuis le fil d'interface.
        envoi.Resultat += resultat => Dispatcher.UIThread.Post(() => Afficher(resultat));
        envoi.PseudosChange += pseudos => Dispatcher.UIThread.Post(() => AfficherPseudos(pseudos));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>« Rapport EUW1_123 accepté. » : vide tant qu'aucun Rapport n'a été traité.</summary>
    public string DernierResultat { get => _dernierResultat; private set => Changer(ref _dernierResultat, value); }

    public bool EchecVisible { get => _echecVisible; private set => Changer(ref _echecVisible, value); }

    public string LigneSuperadministrateur { get => _ligneSuperadministrateur; private set => Changer(ref _ligneSuperadministrateur, value); }

    public string LigneAdministrateur { get => _ligneAdministrateur; private set => Changer(ref _ligneAdministrateur, value); }

    /// <summary>Le texte court de l'infobulle de l'icône de la barre des tâches.</summary>
    public string Infobulle => string.IsNullOrEmpty(DernierResultat) ? "Shameless Companion" : $"Shameless Companion : {DernierResultat}";

    public void OuvrirFichier()
    {
        if (_cheminFichier.Length > 0) _reveler(_cheminFichier);
    }

    public async Task ReessayerAsync() => await _envoi.ReprendreAsync(_arret);

    private void Afficher(ResultatEnvoi resultat)
    {
        DernierResultat = resultat.Message;
        _cheminFichier = resultat.CheminFichier;
        EchecVisible = resultat.ProcedureManuelle;
        Notifier(nameof(Infobulle));
    }

    private void AfficherPseudos(PseudosAdministrateurs? pseudos)
    {
        LigneSuperadministrateur = pseudos is { Superadministrateurs.Count: > 0 } ? string.Join(", ", pseudos.Superadministrateurs) : "";
        LigneAdministrateur = pseudos is { Administrateurs.Count: > 0 } ? string.Join(", ", pseudos.Administrateurs) : "";
    }

    private void Changer<T>(ref T champ, T valeur, [CallerMemberName] string? nom = null)
    {
        if (EqualityComparer<T>.Default.Equals(champ, valeur)) return;
        champ = valeur;
        Notifier(nom);
    }

    private void Notifier(string? nom) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));
}
