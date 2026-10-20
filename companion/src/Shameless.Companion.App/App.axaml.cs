using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Shameless.Companion.Core;
using Shameless.Companion.Lcu;

namespace Shameless.Companion.App;

public sealed class App : Application
{
    private MainWindow? _fenetre;
    private readonly CancellationTokenSource _arret = new();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime bureau)
        {
            // L'app vit dans la barre des tâches : fermer la fenêtre la masque, « Quitter » ferme l'app.
            bureau.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var stockage = CreerStockage();
            var journal = new JournalFichier();
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var connexion = new ConnexionCompanion(stockage, http);
            IClientLol client = new ClientLolLcu(journal: journal);
            // Chronologie (#198) : l'API locale du jeu, lue pendant la partie, jointe au Rapport à la fin.
            var echantillonneur = new EchantillonneurDePartie(new PartieEnCoursLive(), journal);
            var surveillance = new SurveillanceFinDeGame(client, journal, echantillonneur: echantillonneur);
            // Présence (#146) : un émetteur isolé, relié seulement à la détection du client et au Code stocké.
            var presence = new EmetteurPresence(client, stockage, http, journal);
            surveillance.ClientDetecteChange += presence.ClientDetecte;
            var modele = new FenetreViewModel(connexion, stockage, surveillance);

            _fenetre = new MainWindow { DataContext = modele };
            _fenetre.Closing += (_, e) =>
            {
                e.Cancel = true;
                _fenetre.Hide();
            };

            var envoi = BrancheEnvoi.Brancher(surveillance, stockage, http, journal, _arret.Token);
            _fenetre.EncartEnvoi.DataContext = envoi;

            var icone = CreerIcone(bureau);
            envoi.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EnvoiViewModel.Infobulle)) icone.ToolTipText = envoi.Infobulle;
            };
            TrayIcon.SetIcons(this, [icone]);
            _fenetre.Show();
            _ = modele.DemarrerAsync();
            _ = surveillance.ExecuterAsync(_arret.Token);
            _ = presence.ExecuterAsync(_arret.Token);
            _ = echantillonneur.ExecuterAsync(_arret.Token);
            bureau.Exit += (_, _) => _arret.Cancel();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static IStockageIdentifiants CreerStockage() =>
        OperatingSystem.IsWindows() ? StockageDpapi.Par_defaut() : new StockageNonPris();

    private TrayIcon CreerIcone(IClassicDesktopStyleApplicationLifetime bureau)
    {
        var ouvrir = new NativeMenuItem("Ouvrir");
        ouvrir.Click += (_, _) => MontrerFenetre();
        var quitter = new NativeMenuItem("Quitter");
        quitter.Click += (_, _) => bureau.Shutdown();

        var icone = new TrayIcon
        {
            ToolTipText = "Shameless Companion",
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Shameless.Companion.App/Assets/companion.png"))),
            Menu = [ouvrir, quitter],
        };
        icone.Clicked += (_, _) => MontrerFenetre();
        return icone;
    }

    private void MontrerFenetre()
    {
        _fenetre?.Show();
        _fenetre?.Activate();
    }
}

/// <summary>Hors Windows (développement seulement) : rien n'est conservé, plutôt que d'écrire un secret en clair.</summary>
internal sealed class StockageNonPris : IStockageIdentifiants
{
    private IdentifiantsCompanion? _valeur;
    public IdentifiantsCompanion? Lire() => _valeur;
    public void Ecrire(IdentifiantsCompanion identifiants) => _valeur = identifiants;
    public void Effacer() => _valeur = null;
}
