using Shameless.Companion.Core;

namespace Shameless.Companion.App;

/// <summary>
/// Le branchement de l'envoi des Rapports dans l'app (#162) : stockage dans les dossiers de l'app, envoi dès
/// <see cref="SurveillanceFinDeGame.RapportConstruit"/>, renvoi de la file au lancement.
/// <para>Dossier de données (file, pseudos en cache, journal) : <c>LocalApplicationData/Shameless Companion</c>.
/// Fichiers de Rapport : <c>Documents/Shameless Companion/Rapports/rapport-&lt;riotMatchId&gt;.json</c>, un dossier
/// que le joueur retrouve sans effort pour remettre le fichier à la main.</para>
/// </summary>
internal static class BrancheEnvoi
{
    public static EnvoiViewModel Brancher(
        SurveillanceFinDeGame surveillance, IStockageIdentifiants identifiants, HttpClient http, IJournal journal, CancellationToken arret)
    {
        var donnees = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shameless Companion");
        var rapports = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Shameless Companion", "Rapports");

        var envoi = new EnvoiRapports(new StockageEnvoiDisque(donnees, rapports), identifiants, http, journal);
        surveillance.RapportConstruit += rapport => _ = envoi.SoumettreAsync(rapport, arret);
        _ = envoi.DemarrerAsync(arret);
        return new EnvoiViewModel(envoi, arret, OuvreurFichier.Reveler);
    }
}
