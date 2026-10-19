using System.Net;
using System.Text;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shameless.Application.Riot;
using Shameless.Infrastructure.Integrations.Riot;
using Shameless.Infrastructure.Options;

namespace Shameless.Tests.Integration;

/// <summary>Le vrai RiotService sur un faux transport HTTP : ce que voit l'appelant selon la réponse de Riot.</summary>
public class RiotServiceTests
{
    private const string Puuid = "puuid-1";

    private sealed class FauxRiot(Func<HttpRequestMessage, HttpResponseMessage> repondre) : HttpMessageHandler
    {
        public List<string> Appels { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Appels.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(repondre(request));
        }
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Statut(HttpStatusCode code) => new(code);

    private static HttpResponseMessage Limite(int secondes)
    {
        var reponse = Statut(HttpStatusCode.TooManyRequests);
        reponse.Headers.RetryAfter = new(TimeSpan.FromSeconds(secondes));
        return reponse;
    }

    private static RiotService Service(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://europe.example/") };
        http.DefaultRequestHeaders.Add("X-Riot-Token", "cle-de-test");
        var options = Options.Create(new RiotApiOptions
        {
            BaseUrl = "https://europe.example", RegionalBaseUrl = "https://euw.example", RateLimitDelayMs = 0
        });
        return new RiotService(http, options, NullLogger<RiotService>.Instance);
    }

    private static string Match(string role) =>
        $$$"""{"info":{"participants":[{"puuid":"{{{Puuid}}}","teamPosition":"{{{role}}}"}]}}""";

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, CauseEchecRiot.CleRefusee)]
    [InlineData(HttpStatusCode.Unauthorized, CauseEchecRiot.CleRefusee)]
    [InlineData(HttpStatusCode.TooManyRequests, CauseEchecRiot.LimiteAtteinte)]
    [InlineData(HttpStatusCode.InternalServerError, CauseEchecRiot.Panne)]
    [InlineData(HttpStatusCode.NotFound, CauseEchecRiot.Introuvable)]
    public async Task Les_rangs_levent_une_erreur_typee_selon_la_reponse_de_riot(HttpStatusCode statut, CauseEchecRiot cause)
    {
        var service = Service(new FauxRiot(_ => Statut(statut)));

        var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetRanksFromPuuidAsync(Puuid));

        Assert.Equal(cause, erreur.Cause);
        Assert.Equal((int)statut, erreur.Statut);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, CauseEchecRiot.CleRefusee)]
    [InlineData(HttpStatusCode.TooManyRequests, CauseEchecRiot.LimiteAtteinte)]
    [InlineData(HttpStatusCode.InternalServerError, CauseEchecRiot.Panne)]
    [InlineData(HttpStatusCode.NotFound, CauseEchecRiot.Introuvable)]
    public async Task Le_role_leve_une_erreur_typee_quand_la_liste_des_parties_echoue(HttpStatusCode statut, CauseEchecRiot cause)
    {
        var service = Service(new FauxRiot(_ => Statut(statut)));

        var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetMainRoleFromPuuidAsync(Puuid));

        Assert.Equal(cause, erreur.Cause);
    }

    [Fact]
    public async Task Une_limite_atteinte_porte_le_delai_demande_par_riot()
    {
        var service = Service(new FauxRiot(_ => Limite(42)));

        var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetRanksFromPuuidAsync(Puuid));

        Assert.Equal(TimeSpan.FromSeconds(42), erreur.ReessayerApres);
        Assert.Contains("42 s", erreur.Message);
    }

    [Fact]
    public async Task Un_echec_sur_une_partie_ne_donne_pas_un_role_sur_des_donnees_partielles()
    {
        var service = Service(new FauxRiot(r => r.RequestUri!.AbsolutePath.EndsWith("/ids")
            ? Json("""["EUW1_1","EUW1_2","EUW1_3"]""")
            : r.RequestUri.AbsolutePath.EndsWith("EUW1_3") ? Statut(HttpStatusCode.TooManyRequests) : Json(Match("TOP"))));

        var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetMainRoleFromPuuidAsync(Puuid));

        Assert.Equal(CauseEchecRiot.LimiteAtteinte, erreur.Cause);
    }

    [Fact]
    public async Task Une_reponse_sans_partie_donne_aucun_role_sans_erreur()
    {
        var service = Service(new FauxRiot(_ => Json("[]")));

        Assert.Null(await service.GetMainRoleFromPuuidAsync(Puuid));
    }

    [Fact]
    public async Task Le_role_est_le_poste_le_plus_joue()
    {
        var service = Service(new FauxRiot(r => r.RequestUri!.AbsolutePath.EndsWith("/ids")
            ? Json("""["EUW1_1","EUW1_2","EUW1_3"]""")
            : Json(Match(r.RequestUri.AbsolutePath.EndsWith("EUW1_3") ? "TOP" : "JUNGLE"))));

        Assert.Equal("JUNGLE", await service.GetMainRoleFromPuuidAsync(Puuid));
    }

    [Fact]
    public async Task Une_reponse_sans_rang_donne_aucun_rang_sans_erreur()
    {
        var service = Service(new FauxRiot(_ => Json("[]")));

        var rangs = await service.GetRanksFromPuuidAsync(Puuid);

        Assert.Null(rangs.Solo);
        Assert.Null(rangs.Flex);
    }

    [Fact]
    public async Task Un_delai_depasse_est_une_cause_injoignable()
    {
        var service = Service(new FauxRiot(_ => throw new TaskCanceledException("timeout", new TimeoutException())));

        var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetRanksFromPuuidAsync(Puuid));

        Assert.Equal(CauseEchecRiot.Injoignable, erreur.Cause);
    }

    [Fact]
    public async Task Un_compte_inconnu_donne_null_pour_le_puuid_et_le_riot_id()
    {
        var service = Service(new FauxRiot(_ => Statut(HttpStatusCode.NotFound)));

        Assert.Null(await service.GetPuuidFromRiotIdAsync("Inconnu#EUW"));
        Assert.Null(await service.GetRiotIdFromPuuidAsync(Puuid));
    }

    [Fact]
    public async Task Une_cle_refusee_pour_le_puuid_leve_une_erreur_typee()
    {
        var service = Service(new FauxRiot(_ => Statut(HttpStatusCode.Forbidden)));

        var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetPuuidFromRiotIdAsync("Riot#EUW"));

        Assert.Equal(CauseEchecRiot.CleRefusee, erreur.Cause);
    }

    // Le transport complet (les politiques Polly du client Riot) sur le même faux Riot.

    private static (RiotService Service, FauxRiot Riot) AvecPolitiques(Func<HttpRequestMessage, HttpResponseMessage> repondre)
    {
        var faux = new FauxRiot(repondre);
        var retry = new PolicyHttpMessageHandler(RiotHttpPolicies.Retry(_ => TimeSpan.Zero));
        var disjoncteur = new PolicyHttpMessageHandler(RiotHttpPolicies.CircuitBreaker());
        disjoncteur.InnerHandler = faux;
        retry.InnerHandler = disjoncteur;
        return (Service(retry), faux);
    }

    [Fact]
    public async Task Une_cle_refusee_n_est_pas_retentee()
    {
        var (service, riot) = AvecPolitiques(_ => Statut(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<RiotException>(() => service.GetRanksFromPuuidAsync(Puuid));

        Assert.Single(riot.Appels);
    }

    [Fact]
    public async Task Une_limite_dont_le_delai_est_trop_long_n_est_pas_retentee_et_garde_son_delai()
    {
        var (service, riot) = AvecPolitiques(_ => Limite(90));

        var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetRanksFromPuuidAsync(Puuid));

        Assert.Single(riot.Appels);
        Assert.Equal(TimeSpan.FromSeconds(90), erreur.ReessayerApres);
    }

    [Fact]
    public async Task Une_limite_au_delai_court_est_retentee_puis_reussit()
    {
        var n = 0;
        var (service, riot) = AvecPolitiques(_ => ++n > 1 ? Json("[]") : Limite(1));

        await service.GetRanksFromPuuidAsync(Puuid);

        Assert.Equal(2, riot.Appels.Count);
    }

    [Fact]
    public async Task Une_panne_repetee_ouvre_le_disjoncteur_et_la_cause_reste_typee()
    {
        var (service, _) = AvecPolitiques(_ => Statut(HttpStatusCode.InternalServerError));
        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<RiotException>(() => service.GetRanksFromPuuidAsync(Puuid));

        var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetRanksFromPuuidAsync(Puuid));

        Assert.Contains(erreur.Cause, new[] { CauseEchecRiot.Panne, CauseEchecRiot.Injoignable });
    }

    [Fact]
    public async Task Une_limite_atteinte_n_ouvre_pas_le_disjoncteur()
    {
        var (service, _) = AvecPolitiques(_ => Statut(HttpStatusCode.TooManyRequests));
        for (var i = 0; i < 10; i++)
        {
            var erreur = await Assert.ThrowsAsync<RiotException>(() => service.GetRanksFromPuuidAsync(Puuid));
            Assert.Equal(CauseEchecRiot.LimiteAtteinte, erreur.Cause);
        }
    }
}
