using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Integrations.Riot;
using Shameless.Infrastructure.Options;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>
/// Faux Riot : un faux serveur HTTP derrière le vrai <see cref="RiotService"/>, donc les mêmes statuts (403, 429,
/// 5xx, 404) que Riot. Chaque compte est déclaré par le test avec ses rangs par file classée.
/// </summary>
public sealed class FakeRiotService : HttpMessageHandler
{
    private sealed record Compte(string RiotId, string Puuid, string? SoloTier, string? SoloDivision, int SoloLp,
        string? FlexTier, string? FlexDivision, int FlexLp, string? MainRole);

    private readonly Dictionary<string, Compte> _accounts = new();

    public FakeRiotService()
    {
        var http = new HttpClient(this) { BaseAddress = new Uri("https://europe.fake-riot.test/") };
        var options = Options.Create(new RiotApiOptions
        {
            BaseUrl = "https://europe.fake-riot.test", RegionalBaseUrl = "https://euw1.fake-riot.test", RateLimitDelayMs = 0
        });
        Service = new RiotService(http, options, NullLogger<RiotService>.Instance);
    }

    /// <summary>Le vrai RiotService, branché sur ce faux serveur.</summary>
    public IRiotService Service { get; }

    /// <summary>Déclare un compte ; un tier null : pas classé dans la file (SoloQ ou Flex).</summary>
    public void AddAccount(string riotId, string? tier, string? division, int lp = 0, string? mainRole = "MIDDLE",
        string? flexTier = null, string? flexDivision = null, int flexLp = 0) =>
        _accounts[riotId] = new Compte(riotId, $"puuid-{riotId}", tier, division, lp, flexTier, flexDivision, flexLp, mainRole);

    /// <summary>Vrai : l'API Riot répond <see cref="StatutRangs"/> aux rangs.</summary>
    public bool RangsIndisponibles { get; set; }

    /// <summary>Le statut de l'échec des rangs (500 par défaut, 403, 429...).</summary>
    public HttpStatusCode StatutRangs { get; set; } = HttpStatusCode.InternalServerError;

    /// <summary>Les RiotId dont la liste des parties échoue avec <see cref="StatutRoles"/>.</summary>
    public HashSet<string> RolesIndisponibles { get; } = new();

    public HttpStatusCode StatutRoles { get; set; } = HttpStatusCode.InternalServerError;

    /// <summary>Quand elle est posée, la lecture des rangs attend sa libération : fige une actualisation en cours.</summary>
    public TaskCompletionSource? Barriere { get; set; }

    public void Reset()
    {
        _accounts.Clear();
        RolesIndisponibles.Clear();
        RangsIndisponibles = false;
        StatutRangs = HttpStatusCode.InternalServerError;
        StatutRoles = HttpStatusCode.InternalServerError;
        Barriere?.TrySetResult();
        Barriere = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var chemin = request.RequestUri!.AbsolutePath;

        if (chemin.StartsWith("/riot/account/v1/accounts/by-riot-id/"))
        {
            var morceaux = chemin.Split('/');
            var compte = _accounts.GetValueOrDefault($"{Uri.UnescapeDataString(morceaux[^2])}#{Uri.UnescapeDataString(morceaux[^1])}");
            return compte is null ? new(HttpStatusCode.NotFound) : Json(new { puuid = compte.Puuid, gameName = "", tagLine = "" });
        }

        if (chemin.StartsWith("/riot/account/v1/accounts/by-puuid/"))
        {
            var compte = Par(chemin.Split('/')[^1]);
            if (compte is null) return new(HttpStatusCode.NotFound);
            var nom = compte.RiotId.Split('#');
            return Json(new { puuid = compte.Puuid, gameName = nom[0], tagLine = nom[1] });
        }

        if (chemin.StartsWith("/lol/league/v4/entries/by-puuid/"))
        {
            if (Barriere is { } barriere) await barriere.Task;
            if (RangsIndisponibles) return new(StatutRangs);
            var compte = Par(chemin.Split('/')[^1]);
            var files = new List<object>();
            if (compte?.SoloTier is { } solo)
                files.Add(new { queueType = "RANKED_SOLO_5x5", tier = solo, rank = compte.SoloDivision ?? "", leaguePoints = compte.SoloLp });
            if (compte?.FlexTier is { } flex)
                files.Add(new { queueType = "RANKED_FLEX_SR", tier = flex, rank = compte.FlexDivision ?? "", leaguePoints = compte.FlexLp });
            return Json(files);
        }

        if (chemin.Contains("/lol/match/v5/matches/by-puuid/"))
        {
            var compte = Par(chemin.Split('/')[^2]);
            if (compte is not null && RolesIndisponibles.Contains(compte.RiotId)) return new(StatutRoles);
            // Un identifiant de partie sans caractère spécial : l'indice du compte, puis le numéro de la partie.
            var indice = compte is null ? -1 : _accounts.Keys.ToList().IndexOf(compte.RiotId);
            return Json(compte?.MainRole is null ? Array.Empty<string>() : new[] { $"{indice}_1", $"{indice}_2" });
        }

        if (chemin.Contains("/lol/match/v5/matches/"))
        {
            var compte = _accounts.Values.ElementAtOrDefault(int.Parse(chemin.Split('/')[^1].Split('_')[0]));
            return Json(new { info = new { participants = new[] { new { puuid = compte?.Puuid, teamPosition = compte?.MainRole } } } });
        }

        return new(HttpStatusCode.NotFound);
    }

    private Compte? Par(string puuid) =>
        _accounts.Values.FirstOrDefault(a => a.Puuid == Uri.UnescapeDataString(puuid));

    private static HttpResponseMessage Json(object corps) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(corps), Encoding.UTF8, "application/json") };
}
