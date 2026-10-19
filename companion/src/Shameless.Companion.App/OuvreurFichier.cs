using System.Diagnostics;

namespace Shameless.Companion.App;

/// <summary>Ouvre le dossier du Fichier de Rapport avec le fichier sélectionné (Explorateur sous Windows, Finder sur macOS).</summary>
internal static class OuvreurFichier
{
    public static void Reveler(string chemin)
    {
        var demarrage = new ProcessStartInfo { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            demarrage.FileName = "explorer.exe";
            // explorer veut « /select,<chemin> » en un seul argument.
            demarrage.ArgumentList.Add($"/select,{chemin}");
        }
        else if (OperatingSystem.IsMacOS())
        {
            demarrage.FileName = "open";
            demarrage.ArgumentList.Add("-R");
            demarrage.ArgumentList.Add(chemin);
        }
        else
        {
            // Linux (développement) : pas de sélection standard, on ouvre le dossier.
            demarrage.FileName = "xdg-open";
            demarrage.ArgumentList.Add(Path.GetDirectoryName(chemin) ?? chemin);
        }

        try
        {
            Process.Start(demarrage)?.Dispose();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Pas d'explorateur disponible : le chemin reste affiché dans la fenêtre.
        }
    }
}
