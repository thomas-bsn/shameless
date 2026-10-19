namespace Shameless.Companion.Lcu;

/// <summary>Contenu du lockfile du client LoL : <c>nom:pid:port:mot-de-passe:protocole</c>.</summary>
public sealed record Lockfile(string Name, int Pid, int Port, string Password, string Protocol)
{
    public const string DefaultPath = @"C:\Riot Games\League of Legends\lockfile";

    /// <summary>
    /// Choisit le lockfile : le chemin explicite (--lockfile) s'il est donné, sinon celui du dossier d'un
    /// client LoL en cours d'exécution, sinon le chemin par défaut. Rien n'est cherché dans les disques.
    /// </summary>
    public static string Locate(string? explicitPath, IEnumerable<string> clientDirectories, string defaultPath = DefaultPath)
    {
        if (!string.IsNullOrEmpty(explicitPath)) return explicitPath;

        foreach (var dir in clientDirectories)
        {
            var candidate = Path.Combine(dir, "lockfile");
            if (File.Exists(candidate)) return candidate;
        }

        return defaultPath;
    }

    /// <summary>
    /// Dossiers des clients LoL en cours d'exécution (le lockfile est à côté de leur exécutable).
    /// Un processus dont le chemin est illisible (droits) est ignoré : le chemin par défaut reste le repli.
    /// </summary>
    public static IReadOnlyList<string> RunningClientDirectories()
    {
        var dirs = new List<string>();
        foreach (var name in new[] { "LeagueClientUx", "LeagueClient" })
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(name))
            {
                try
                {
                    var exe = process.MainModule?.FileName;
                    var dir = exe is null ? null : Path.GetDirectoryName(exe);
                    if (dir is not null && !dirs.Contains(dir)) dirs.Add(dir);
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // Accès refusé (client lancé en administrateur) ou processus déjà terminé.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        return dirs;
    }

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
