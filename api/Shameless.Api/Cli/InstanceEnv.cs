using System.Text;

namespace Shameless.Api.Cli;

/// <summary>
/// Le fichier <c>instance.env</c> écrit par le Wizard (<c>CLE='valeur'</c>, apostrophe échappée en <c>'\''</c>).
/// Le conteneur de l'API le charge dans son environnement avant de démarrer ; une commande lancée par
/// <c>docker compose exec</c> ne passe pas par là, donc la commande le relit elle-même.
/// </summary>
public static class InstanceEnv
{
    public const string DefaultPath = "/instance/instance.env";

    public static Dictionary<string, string> Parse(string content)
    {
        var values = new Dictionary<string, string>();
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (line.Length == 0 || line[0] == '#' || eq <= 0) continue;
            values[line[..eq].Trim()] = Unquote(line[(eq + 1)..]);
        }
        return values;
    }

    private static string Unquote(string value)
    {
        var result = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\'')
            {
                var end = value.IndexOf('\'', i + 1);
                if (end < 0) end = value.Length;
                result.Append(value, i + 1, end - i - 1);
                i = end;
            }
            else if (value[i] == '\\' && i + 1 < value.Length)
                result.Append(value[++i]);
            else
                result.Append(value[i]);
        }
        return result.ToString();
    }

    /// <summary>
    /// La configuration de la commande : environnement, complété par <c>instance.env</c> pour ce que l'environnement
    /// ne définit pas (<c>SHAMELESS_INSTANCE_ENV</c> change son chemin).
    /// </summary>
    public static IConfiguration Load(string? path = null)
    {
        path ??= Environment.GetEnvironmentVariable("SHAMELESS_INSTANCE_ENV") ?? DefaultPath;
        var builder = new ConfigurationBuilder();
        if (File.Exists(path))
            builder.AddInMemoryCollection(Parse(File.ReadAllText(path))
                .ToDictionary(kv => kv.Key.Replace("__", ":"), kv => (string?)kv.Value));
        builder.AddEnvironmentVariables();
        return builder.Build();
    }
}
