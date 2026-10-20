using System.Text.Json;

namespace Shameless.Companion.Core;

/// <summary>
/// Lit l'API locale du jeu environ toutes les 10 s (<see cref="IPartieEnCours"/>) et garde les lectures de la partie
/// en cours. À la fin de la game, <see cref="Prendre"/> les rattache aux PUUID du Rapport (ADR 0022). Aucun calcul :
/// des lectures datées. Indisponible ou format inattendu : pas de chronologie, jamais d'erreur.
/// </summary>
public sealed class EchantillonneurDePartie(IPartieEnCours partie, IJournal journal, TimeSpan? intervalle = null)
{
    public const int NombreMaxDePoints = 2000;
    public const int NombreMaxDEvenements = 500;

    /// <summary>L'API locale se ferme avec la partie : le dernier point doit tomber près de la durée annoncée par le bloc de fin de game.</summary>
    private static readonly TimeSpan MargeAvant = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MargeApres = TimeSpan.FromSeconds(30);

    private enum Etat { Inconnu, Indisponible, Inattendu, Lisible }

    private readonly TimeSpan _intervalle = intervalle ?? TimeSpan.FromSeconds(10);
    private readonly object _verrou = new();
    private readonly List<(int T, IReadOnlyList<JoueurEnPartie> Joueurs)> _points = [];
    private IReadOnlyList<EvenementDeChronologie> _evenements = [];
    private int _dernierTemps = -1;
    private Etat _etat = Etat.Inconnu;

    /// <summary>Tourne jusqu'à l'annulation.</summary>
    public async Task ExecuterAsync(CancellationToken ct)
    {
        using var minuteur = new PeriodicTimer(_intervalle);
        try
        {
            do await EchantillonnerAsync(ct);
            while (await minuteur.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    /// <summary>Une lecture. Ne lève jamais, sauf à l'annulation.</summary>
    public async Task EchantillonnerAsync(CancellationToken ct)
    {
        string? json;
        try
        {
            json = await partie.LireAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            json = null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            ChangerEtat(Etat.Indisponible, "API locale du jeu indisponible");
            return;
        }

        var instantane = LiveClient.Lire(json);
        if (instantane is null)
        {
            ChangerEtat(Etat.Inattendu, "API locale du jeu : format inattendu, pas de chronologie pour cette partie");
            return;
        }
        ChangerEtat(Etat.Lisible, "API locale du jeu lisible");

        lock (_verrou)
        {
            // Le temps recule : une autre partie a commencé.
            if (instantane.Temps < _dernierTemps) _points.Clear();
            _dernierTemps = instantane.Temps;
            _evenements = instantane.Evenements;

            if (_points.Count < NombreMaxDePoints && (_points.Count == 0 || instantane.Temps > _points[^1].T))
                _points.Add((instantane.Temps, instantane.Joueurs));
        }
    }

    private void ChangerEtat(Etat nouveau, string message)
    {
        lock (_verrou)
        {
            if (_etat == nouveau) return;
            var ancien = _etat;
            _etat = nouveau;
            // Hors partie l'API est toujours fermée : le silence est normal tant qu'on n'a rien lu.
            if (nouveau == Etat.Indisponible && ancien != Etat.Lisible) return;
            journal.Ecrire(message);
        }
    }

    /// <summary>
    /// La chronologie de la partie qui vient de finir, rattachée aux PUUID du bloc de fin de game ; null s'il n'y en a pas
    /// de fiable. L'enregistrement est consommé : il ne sert qu'une fois.
    /// </summary>
    public ChronologieDuRapport? Prendre(string eogJson)
    {
        List<(int T, IReadOnlyList<JoueurEnPartie> Joueurs)> points;
        IReadOnlyList<EvenementDeChronologie> evenements;
        lock (_verrou)
        {
            points = [.. _points];
            evenements = _evenements;
            _points.Clear();
            _evenements = [];
            _dernierTemps = -1;
        }
        if (points.Count == 0) return null;

        try
        {
            using var eog = JsonDocument.Parse(eogJson);
            if (!eog.RootElement.TryGetProperty("gameLength", out var duree) || !duree.TryGetInt32(out var secondes))
            {
                journal.Ecrire("Chronologie écartée : durée de la partie absente du bloc de fin de game");
                return null;
            }
            var dernier = points[^1].T;
            if (dernier < secondes - MargeAvant.TotalSeconds || dernier > secondes + MargeApres.TotalSeconds)
            {
                journal.Ecrire($"Chronologie écartée : dernière lecture à {dernier} s, la partie a duré {secondes} s");
                return null;
            }

            var puuids = PuuidsParNom(eog.RootElement);
            var rattaches = new List<PointDeChronologie>();
            foreach (var (t, joueurs) in points)
            {
                var duPoint = new List<JoueurDuPoint>();
                foreach (var joueur in joueurs)
                {
                    if (joueur.Valeurs is not { } v) continue;
                    var puuid = joueur.Cles.Select(c => puuids.GetValueOrDefault(c)).FirstOrDefault(p => p is not null);
                    if (puuid is not null && duPoint.All(d => d.Puuid != puuid))
                        duPoint.Add(new JoueurDuPoint(puuid, v.Kills, v.Deaths, v.Assists, v.Cs, v.Level, v.Vision, v.ItemsValue, v.Dead));
                }
                if (duPoint.Count > 0) rattaches.Add(new PointDeChronologie(t, duPoint));
            }

            if (rattaches.Count == 0)
            {
                journal.Ecrire("Chronologie écartée : aucun joueur de la partie n'a pu être rattaché à un PUUID");
                return null;
            }
            return new ChronologieDuRapport(rattaches, [.. evenements.Take(NombreMaxDEvenements)]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Riot ID et pseudo (sans casse) vers PUUID, pour les joueurs dont le PUUID est unique dans la partie (les bots partagent le leur) et le nom aussi.</summary>
    private static Dictionary<string, string> PuuidsParNom(JsonElement eog)
    {
        var joueurs = new List<(string Puuid, List<string> Noms)>();
        if (eog.TryGetProperty("teams", out var equipes) && equipes.ValueKind == JsonValueKind.Array)
            foreach (var equipe in equipes.EnumerateArray())
                if (equipe.ValueKind == JsonValueKind.Object && equipe.TryGetProperty("players", out var liste) && liste.ValueKind == JsonValueKind.Array)
                    foreach (var j in liste.EnumerateArray())
                    {
                        var puuid = Texte(j, "puuid");
                        var nom = Texte(j, "riotIdGameName");
                        var tag = Texte(j, "riotIdTagLine");
                        var noms = new List<string>();
                        if (!string.IsNullOrEmpty(nom) && !string.IsNullOrEmpty(tag)) noms.Add($"{nom}#{tag}");
                        if (Texte(j, "summonerName") is { Length: > 0 } pseudo) noms.Add(pseudo);
                        joueurs.Add((puuid ?? "", noms));
                    }

        var parPuuid = joueurs.GroupBy(j => j.Puuid).ToDictionary(g => g.Key, g => g.Count());
        var resultat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ambigus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (puuid, noms) in joueurs.Where(j => j.Puuid.Length > 0 && parPuuid[j.Puuid] == 1))
            foreach (var nom in noms.Distinct(StringComparer.OrdinalIgnoreCase))
                if (!resultat.TryAdd(nom, puuid)) ambigus.Add(nom);
        foreach (var nom in ambigus) resultat.Remove(nom);
        return resultat;
    }

    private static string? Texte(JsonElement objet, string cle) =>
        objet.ValueKind == JsonValueKind.Object && objet.TryGetProperty(cle, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
