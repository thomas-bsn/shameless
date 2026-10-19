using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Shameless.Companion.Core;

namespace Shameless.Companion.App;

/// <summary>Colle la bibliothèque (ConnexionCompanion) à la fenêtre : de la présentation, pas de logique.</summary>
public sealed class FenetreViewModel : INotifyPropertyChanged
{
    private readonly ConnexionCompanion _connexion;
    private readonly IStockageIdentifiants _stockage;
    private string _urlInstance = "";
    private string _code = "";
    private string _etatConnexion = "Non connecté";
    private string _etatClientLol = LibelleClientLol(false);
    private string _serie = "";
    private bool _occupe;

    public FenetreViewModel(ConnexionCompanion connexion, IStockageIdentifiants stockage, SurveillanceFinDeGame surveillance)
    {
        _connexion = connexion;
        _stockage = stockage;
        // L'événement arrive d'un fil d'arrière-plan : la fenêtre ne se met à jour que depuis le fil d'interface.
        surveillance.ClientDetecteChange += detecte =>
            Dispatcher.UIThread.Post(() => EtatClientLol = LibelleClientLol(detecte));
    }

    private static string LibelleClientLol(bool detecte) =>
        detecte ? "Client LoL : détecté" : "Client LoL : non détecté";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string UrlInstance { get => _urlInstance; set => Changer(ref _urlInstance, value); }
    public string Code { get => _code; set => Changer(ref _code, value); }
    public string EtatConnexion { get => _etatConnexion; private set => Changer(ref _etatConnexion, value); }
    public string EtatClientLol { get => _etatClientLol; private set => Changer(ref _etatClientLol, value); }
    public string Serie { get => _serie; private set => Changer(ref _serie, value); }
    public bool Occupe { get => _occupe; private set => Changer(ref _occupe, value); }

    public async Task DemarrerAsync()
    {
        var identifiants = _stockage.Lire();
        if (identifiants is not null)
        {
            UrlInstance = identifiants.UrlInstance;
            Code = identifiants.Code;
        }
        await Executer(() => _connexion.DemarrerAsync());
    }

    public Task ConnecterAsync() =>
        Executer(() => _connexion.SeConnecterAsync(UrlInstance.Trim(), Code.Trim()));

    private async Task Executer(Func<Task<EtatCompanion>> action)
    {
        Occupe = true;
        try
        {
            Afficher(await action());
        }
        finally
        {
            Occupe = false;
        }
    }

    private void Afficher(EtatCompanion etat)
    {
        switch (etat.Connexion)
        {
            case Connexion.SerieReconnue reconnue:
                var s = reconnue.Serie;
                EtatConnexion = "Série reconnue";
                Serie = $"Série {s.SeriesId} : BO{s.Format}{(s.Fearless ? " Fearless" : "")}, {s.TeamAWins}-{s.TeamBWins}\n"
                    + $"Équipe A : {string.Join(", ", s.EquipeA)}\nÉquipe B : {string.Join(", ", s.EquipeB)}";
                break;
            case Connexion.CodeRefuse:
                Code = "";
                EtatConnexion = "Code refusé par l'API (inconnu, renouvelé ou Série terminée) : il a été effacé.";
                Serie = "";
                break;
            case Connexion.Injoignable injoignable:
                EtatConnexion = $"API injoignable : {injoignable.Message}";
                Serie = "";
                break;
            default:
                EtatConnexion = "Non connecté";
                Serie = "";
                break;
        }
    }

    private void Changer<T>(ref T champ, T valeur, [CallerMemberName] string? nom = null)
    {
        if (EqualityComparer<T>.Default.Equals(champ, valeur)) return;
        champ = valeur;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));
    }
}
