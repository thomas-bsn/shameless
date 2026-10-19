namespace Spike;

/// <summary>Contenu du lockfile du client LoL : <c>nom:pid:port:mot-de-passe:protocole</c>.</summary>
public sealed record Lockfile(string Name, int Pid, int Port, string Password, string Protocol)
{
    public const string DefaultPath = @"C:\Riot Games\League of Legends\lockfile";

    public static Lockfile Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Lockfile introuvable : {path}. Le client LoL est-il lancé ? Sinon, passe le chemin en argument (--lockfile).", path);

        // Le client garde le fichier ouvert : lecture partagée obligatoire.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd().Trim();

        var parts = content.Split(':');
        if (parts.Length != 5 || !int.TryParse(parts[1], out var pid) || !int.TryParse(parts[2], out var port))
            throw new FormatException($"Lockfile illisible ({path}) : attendu nom:pid:port:mot-de-passe:protocole.");

        return new Lockfile(parts[0], pid, port, parts[3], parts[4]);
    }
}
