using Shameless.Companion.Core;

namespace Shameless.Companion.App;

/// <summary>Le journal du Companion : un fichier texte dans le profil du joueur (parties ignorées, abandons, détection du client).</summary>
internal sealed class JournalFichier : IJournal
{
    private readonly object _verrou = new();
    private readonly string _chemin = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shameless Companion", "journal.log");

    public void Ecrire(string ligne)
    {
        var texte = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {ligne}{Environment.NewLine}";
        System.Diagnostics.Debug.Write(texte);
        try
        {
            lock (_verrou)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_chemin)!);
                File.AppendAllText(_chemin, texte);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Un journal illisible ne doit jamais arrêter le Companion.
        }
    }
}
