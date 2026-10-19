using System.Text.Json;

namespace Shameless.Companion.Core;

/// <param name="Essais">Nombre de lectures avant d'abandonner (l'historique peut mettre 5 à 15 s à répondre).</param>
public sealed record OptionsSurveillance(int Essais, TimeSpan DelaiEntreEssais)
{
    public static OptionsSurveillance ParDefaut { get; } = new(10, TimeSpan.FromSeconds(3));
}

/// <summary>
/// Écoute le client LoL (via <see cref="IClientLol"/>) et, à chaque fin de game, lit le bloc de fin de game puis
/// la game de l'historique pour construire le Rapport. Une seule fois par gameId. L'envoi du Rapport n'est pas ici :
/// #162 se branche sur <see cref="RapportConstruit"/>.
/// </summary>
public sealed class SurveillanceFinDeGame(IClientLol client, IJournal journal, OptionsSurveillance? options = null)
{
    private const string UriBlocFinDeGame = "/lol-end-of-game/v1/eog-stats-block";
    private const string UriPhase = "/lol-gameflow/v1/gameflow-phase";

    private readonly OptionsSurveillance _options = options ?? OptionsSurveillance.ParDefaut;
    private readonly HashSet<long> _gamesTraitees = [];

    /// <summary>Un Rapport est prêt à partir (tel que lu : aucun calcul).</summary>
    public event Action<RapportDeGame>? RapportConstruit;

    /// <summary>Le client LoL vient d'être détecté (true) ou perdu (false).</summary>
    public event Action<bool>? ClientDetecteChange;

    /// <summary>Tourne jusqu'à l'annulation (ou la fin du flux d'événements), puis attend les lectures en cours.</summary>
    public async Task ExecuterAsync(CancellationToken ct)
    {
        var lectures = new List<Task>();
        try
        {
            await foreach (var evt in client.SuivreAsync(ct))
            {
                switch (evt)
                {
                    case EvenementClientLol.Detecte:
                        journal.Ecrire("Client LoL détecté");
                        ClientDetecteChange?.Invoke(true);
                        break;
                    case EvenementClientLol.Perdu:
                        journal.Ecrire("Client LoL perdu");
                        ClientDetecteChange?.Invoke(false);
                        break;
                    case EvenementClientLol.Message message when EstFinDeGame(message):
                        lectures.Add(Task.Run(() => TraiterFinDeGameAsync(ct), CancellationToken.None));
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        await Task.WhenAll(lectures);
    }

    private static bool EstFinDeGame(EvenementClientLol.Message message)
    {
        if (message.TypeEvenement == "Delete") return false;
        if (message.Uri == UriBlocFinDeGame) return true;
        return message.Uri == UriPhase
               && message.Donnees.ValueKind == JsonValueKind.String
               && message.Donnees.GetString() == "EndOfGame";
    }

    private async Task TraiterFinDeGameAsync(CancellationToken ct)
    {
        try
        {
            journal.Ecrire("Fin de game détectée : lecture du bloc de fin de game");
            var eog = await LireAvecEssaisAsync(UriBlocFinDeGame, ct);
            if (eog is null)
            {
                journal.Ecrire("Fin de game abandonnée : le bloc de fin de game n'a jamais répondu.");
                return;
            }

            var raison = ConstructeurRapport.RaisonDIgnorer(eog);
            if (raison is not null)
            {
                journal.Ecrire($"Fin de game, partie ignorée : {raison}");
                return;
            }

            var gameId = LireGameId(eog);
            if (gameId is null)
            {
                journal.Ecrire("Fin de game abandonnée : pas de gameId dans le bloc de fin de game.");
                return;
            }

            lock (_gamesTraitees)
                if (!_gamesTraitees.Add(gameId.Value))
                {
                    journal.Ecrire($"Game {gameId} déjà traitée : événement ignoré.");
                    return;
                }

            var historique = await LireAvecEssaisAsync($"/lol-match-history/v1/games/{gameId}", ct);
            if (historique is null)
            {
                // Un prochain événement pour cette game pourra retenter.
                lock (_gamesTraitees) _gamesTraitees.Remove(gameId.Value);
                journal.Ecrire($"Game {gameId} abandonnée : l'historique n'a pas répondu après {_options.Essais} essais, pas de platformId.");
                return;
            }

            switch (ConstructeurRapport.Construire(eog, historique))
            {
                case ResultatRapport.Ignore ignore:
                    journal.Ecrire($"Fin de game, partie ignorée : {ignore.Raison}");
                    break;
                case ResultatRapport.Construit construit:
                    var rapport = construit.Rapport;
                    var sansPuuid = rapport.JoueursSansPuuid.Count;
                    if (sansPuuid > 0)
                        journal.Ecrire($"Game {gameId} : {sansPuuid} joueur(s) sans PUUID dans le Rapport (il partira en revue côté API).");
                    journal.Ecrire($"Rapport construit : {rapport.RiotMatchId}, {rapport.Joueurs.Count} joueurs");
                    RapportConstruit?.Invoke(rapport);
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            journal.Ecrire($"Fin de game abandonnée : {e.Message}");
        }
    }

    private async Task<string?> LireAvecEssaisAsync(string chemin, CancellationToken ct)
    {
        for (var essai = 1; essai <= _options.Essais; essai++)
        {
            try
            {
                var reponse = await client.LireAsync(chemin, ct);
                if (reponse.Statut == 200 && reponse.Corps.Length > 2) return reponse.Corps;
            }
            catch (HttpRequestException e)
            {
                journal.Ecrire($"GET {chemin} : erreur réseau ({e.Message}), essai {essai}/{_options.Essais}");
            }
            if (essai < _options.Essais) await Task.Delay(_options.DelaiEntreEssais, ct);
        }
        return null;
    }

    private static long? LireGameId(string eog)
    {
        using var doc = JsonDocument.Parse(eog);
        return doc.RootElement.TryGetProperty("gameId", out var id) && id.TryGetInt64(out var valeur) ? valeur : null;
    }
}
