using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Shameless.Companion.App;

public sealed partial class EncartEnvoi : UserControl
{
    public EncartEnvoi() => InitializeComponent();

    private void OnOuvrirFichier(object? sender, RoutedEventArgs e) => (DataContext as EnvoiViewModel)?.OuvrirFichier();

    private async void OnReessayer(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EnvoiViewModel modele) await modele.ReessayerAsync();
    }
}
