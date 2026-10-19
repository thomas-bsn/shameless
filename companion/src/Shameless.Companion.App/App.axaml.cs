using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Shameless.Companion.Core;

namespace Shameless.Companion.App;

public sealed class App : Application
{
    private MainWindow? _fenetre;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime bureau)
        {
            // L'app vit dans la barre des tâches : fermer la fenêtre la masque, « Quitter » ferme l'app.
            bureau.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var stockage = CreerStockage();
            var connexion = new ConnexionCompanion(stockage, new HttpClient { Timeout = TimeSpan.FromSeconds(15) });
            var modele = new FenetreViewModel(connexion, stockage);

            _fenetre = new MainWindow { DataContext = modele };
            _fenetre.Closing += (_, e) =>
            {
                e.Cancel = true;
                _fenetre.Hide();
            };

            TrayIcon.SetIcons(this, [CreerIcone(bureau)]);
            _fenetre.Show();
            _ = modele.DemarrerAsync();
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
