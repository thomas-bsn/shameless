using System.Text.Json;

namespace Shameless.Companion.Core;

/// <summary>
/// Le stockage de l'envoi sur disque : la file et le cache des pseudos dans <c>dossierDonnees</c>
/// (<c>file-envoi.json</c>, <c>administrateurs.json</c>), les Fichiers de Rapport dans <c>dossierRapports</c>.
/// </summary>
public sealed class StockageEnvoiDisque(string dossierDonnees, string dossierRapports) : IStockageEnvoi
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private string CheminFile => Path.Combine(dossierDonnees, "file-envoi.json");
    private string CheminPseudos => Path.Combine(dossierDonnees, "administrateurs.json");

    public string EcrireFichierRapport(string riotMatchId, string json)
    {
        Directory.CreateDirectory(dossierRapports);
        var nomSur = string.Concat(riotMatchId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var chemin = Path.Combine(dossierRapports, $"rapport-{nomSur}.json");
        File.WriteAllText(chemin, json);
        return chemin;
    }

    public IReadOnlyList<RapportEnFile> LireFile() => Lire<List<RapportEnFile>>(CheminFile) ?? [];

    public void EcrireFile(IReadOnlyList<RapportEnFile> file) => Ecrire(CheminFile, file);

    public PseudosAdministrateurs? LirePseudos() => Lire<PseudosAdministrateurs>(CheminPseudos);

    public void EcrirePseudos(PseudosAdministrateurs pseudos) => Ecrire(CheminPseudos, pseudos);

    private static T? Lire<T>(string chemin)
    {
        try
        {
            return File.Exists(chemin) ? JsonSerializer.Deserialize<T>(File.ReadAllText(chemin), Options) : default;
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return default;
        }
    }

    private void Ecrire<T>(string chemin, T valeur)
    {
        Directory.CreateDirectory(dossierDonnees);
        // Écriture atomique : un arrêt en plein milieu ne doit pas perdre la file.
        var temporaire = chemin + ".tmp";
        File.WriteAllText(temporaire, JsonSerializer.Serialize(valeur, Options));
        File.Move(temporaire, chemin, overwrite: true);
    }
}
