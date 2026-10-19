using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Shameless.Companion.App;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void OnConnecter(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FenetreViewModel modele) await modele.ConnecterAsync();
    }
}
