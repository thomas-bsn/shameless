using Spike;

const string Usage = """
    shameless-spike : capture des JSON de fin de game du client LoL (jetable, ticket #158)

    Capturer (client LoL lancé) :
      shameless-spike [--lockfile <chemin>] [--out <dossier>] [--retries <n>] [--retry-delay <secondes>] [--raw-all]
        --lockfile     défaut : le dossier du client LoL en cours d'exécution, sinon C:\Riot Games\League of Legends\lockfile
        --out          défaut : captures
        --retries      essais pour l'historique (défaut 10)
        --retry-delay  secondes entre deux essais (défaut 3)
        --raw-all      enregistre tous les événements WebSocket, pas seulement la fin de game

    Anonymiser des captures avant de les mettre dans le dépôt :
      shameless-spike anonymize <dossier-source> <dossier-destination>
    """;

if (args.Length > 0 && args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    return 0;
}

if (args.Length > 0 && args[0] == "anonymize")
{
    if (args.Length != 3)
    {
        Console.Error.WriteLine(Usage);
        return 2;
    }
    Anonymizer.AnonymizeDirectory(args[1], args[2]);
    Console.WriteLine($"Captures anonymisées dans {args[2]}. Relis-les (pseudos dans des champs inattendus ?) avant de les committer.");
    return 0;
}

var options = new SpikeOptions();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--lockfile" when i + 1 < args.Length: options.LockfilePath = args[++i]; break;
        case "--out" when i + 1 < args.Length: options.OutputRoot = args[++i]; break;
        case "--retries" when i + 1 < args.Length && int.TryParse(args[i + 1], out var n): options.RetryCount = n; i++; break;
        case "--retry-delay" when i + 1 < args.Length && double.TryParse(args[i + 1], out var s):
            options.RetryDelay = TimeSpan.FromSeconds(s); i++; break;
        case "--raw-all": options.RawAllEvents = true; break;
        default:
            Console.Error.WriteLine($"Argument inconnu : {args[i]}\n\n{Usage}");
            return 2;
    }
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var captures = await SpikeRunner.RunAsync(options, cts.Token);
    Console.WriteLine($"Terminé : {captures.Count} capture(s) dans {Path.GetFullPath(options.OutputRoot)}");
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Arrêté.");
    return 0;
}
catch (Exception e) when (e is FileNotFoundException or FormatException or InvalidOperationException)
{
    Console.Error.WriteLine($"ERREUR : {e.Message}");
    return 1;
}
