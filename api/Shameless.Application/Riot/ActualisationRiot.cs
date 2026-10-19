using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shameless.Application.BackgroundJobs;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Riot;

public sealed record EchecActualisation(int PlayerId, string Prenom, string Message);

/// <summary>Pourquoi Riot a échoué pour une lecture : le texte à afficher, le code, et si insister est inutile.</summary>
public sealed record EchecRiotLisible(string Texte, CauseEchecRiot? Cause, bool Bloque);

/// <summary>
/// Ce qu'une actualisation a fait pour un joueur ; les échecs gardent les anciennes valeurs.
/// <paramref name="CauseRangs"/> et <paramref name="CauseRole"/> disent en français pourquoi Riot a échoué (null si
/// tout va bien), <paramref name="CodeCauseRangs"/> et <paramref name="CodeCauseRole"/> en donnent le code (null pour
/// une erreur qui ne vient pas de Riot) ; <paramref name="Echecs"/> en fait des messages prêts à afficher.
/// <paramref name="RiotBloque"/> : clé refusée ou attente trop longue, inutile d'interroger Riot pour d'autres joueurs.
/// </summary>
public sealed record ResultatActualisationJoueur(
    int PlayerId, string Prenom, bool RangsActualises, bool RoleActualise, IReadOnlyList<string> Echecs,
    string? CauseRangs = null, string? CauseRole = null,
    CauseEchecRiot? CodeCauseRangs = null, CauseEchecRiot? CodeCauseRole = null, bool RiotBloque = false);

/// <summary>La progression de la dernière actualisation, lue par l'Administration.</summary>
public sealed record EtatActualisationRiot(
    bool Disponible,
    bool EnCours,
    string? Nature,
    int Total,
    int Termines,
    IReadOnlyList<EchecActualisation> Echecs,
    DateTimeOffset? DemarreeLe,
    DateTimeOffset? TermineeLe);

/// <summary>
/// Le verrou unique des lectures Riot (rangs, rôle principal) : un administrateur, « Tout actualiser » et la
/// tâche de 24 h passent tous par lui, donc un seul tourne à la fois. Il porte aussi la progression.
/// </summary>
public sealed class ActualisationRiot(
    IServiceScopeFactory scopes,
    IOptions<RefreshRanksJobOptions> options,
    TimeProvider clock,
    ILogger<ActualisationRiot> logger)
{
    private readonly object _verrou = new();
    private bool _enCours;
    private string? _nature;
    private int _total;
    private int _termines;
    private List<EchecActualisation> _echecs = [];
    private DateTimeOffset? _demarreeLe;
    private DateTimeOffset? _termineeLe;

    /// <summary>Faux sans clé Riot : rien ne peut être lancé.</summary>
    public bool Disponible => options.Value.Enabled;

    public EtatActualisationRiot Etat()
    {
        lock (_verrou)
            return new EtatActualisationRiot(Disponible, _enCours, _nature, _total, _termines, _echecs.ToList(), _demarreeLe, _termineeLe);
    }

    /// <summary>Exécute le travail s'il n'y a pas déjà un lancement ; faux (sans rien faire) sinon.</summary>
    public async Task<bool> ExecuterSiLibreAsync(string nature, Func<Task> travail)
    {
        if (!Prendre(nature, 0)) return false;
        try { await travail(); }
        finally { Liberer(); }
        return true;
    }

    /// <summary>Actualise un joueur ; Libre faux si un autre lancement tourne.</summary>
    public async Task<(bool Libre, ResultatActualisationJoueur? Resultat)> ActualiserJoueurAsync(int playerId)
    {
        if (!Prendre("Un joueur", 1)) return (false, null);
        try
        {
            using var scope = scopes.CreateScope();
            var resultat = await scope.ServiceProvider.GetRequiredService<IPlayerService>().RefreshRiotAsync(playerId);
            if (resultat is not null) Noter(resultat);
            lock (_verrou) _termines = 1;
            return (true, resultat);
        }
        finally { Liberer(); }
    }

    /// <summary>Lance l'actualisation de tous les joueurs en tâche de fond ; faux si un autre lancement tourne.</summary>
    public async Task<bool> DemarrerTousAsync()
    {
        if (!Prendre("Tous les joueurs", 0)) return false;
        List<(int Id, string Prenom)> joueurs;
        try
        {
            using var scope = scopes.CreateScope();
            joueurs = (await scope.ServiceProvider.GetRequiredService<IPlayerService>().GetPlayersAsync())
                .Where(p => !string.IsNullOrWhiteSpace(p.Puuid))
                .Select(p => (p.Id, p.Prenom))
                .ToList();
            lock (_verrou) _total = joueurs.Count;
        }
        catch
        {
            Liberer();
            throw;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                string? raisonDeLArret = null;
                foreach (var (id, prenom) in joueurs)
                {
                    if (raisonDeLArret is not null)
                    {
                        lock (_verrou)
                        {
                            _echecs.Add(new EchecActualisation(id, prenom,
                                $"Non tentée : actualisation interrompue, Riot est bloqué ({raisonDeLArret}). Corrigez la cause puis relancez."));
                            _termines++;
                        }
                        continue;
                    }
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var resultat = await scope.ServiceProvider.GetRequiredService<IPlayerService>().RefreshRiotAsync(id);
                        if (resultat is not null)
                        {
                            Noter(resultat);
                            // Clé refusée ou attente trop longue : les joueurs suivants échoueraient de la même façon.
                            if (resultat.RiotBloque)
                                raisonDeLArret = resultat.CauseRangs ?? resultat.CauseRole ?? "cause inconnue";
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Riot refresh failed for player {PlayerId}", id);
                        lock (_verrou) _echecs.Add(new EchecActualisation(id, prenom, "Actualisation interrompue par une erreur."));
                    }
                    lock (_verrou) _termines++;
                }
            }
            finally { Liberer(); }
        });
        return true;
    }

    private bool Prendre(string nature, int total)
    {
        lock (_verrou)
        {
            if (_enCours) return false;
            _enCours = true;
            _nature = nature;
            _total = total;
            _termines = 0;
            _echecs = [];
            _demarreeLe = clock.GetUtcNow();
            _termineeLe = null;
            return true;
        }
    }

    private void Liberer()
    {
        lock (_verrou)
        {
            _enCours = false;
            _termineeLe = clock.GetUtcNow();
        }
    }

    private void Noter(ResultatActualisationJoueur resultat)
    {
        lock (_verrou)
        {
            foreach (var message in resultat.Echecs)
                _echecs.Add(new EchecActualisation(resultat.PlayerId, resultat.Prenom, message));
        }
    }
}
