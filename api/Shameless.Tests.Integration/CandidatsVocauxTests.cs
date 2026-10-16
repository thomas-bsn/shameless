using System.Net;
using System.Net.Http.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Les candidats vocaux du Picker : l'API demande au bot Discord, avec sa propre clé, qui est en vocal,
/// et résout les IDs Discord en Joueurs liés. Le navigateur ne voit jamais la clé.
/// </summary>
public class CandidatsVocauxTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Les_membres_en_vocal_lies_a_un_joueur_sont_proposes_au_picker()
    {
        var organisateur = Factory.CreateSignedInClient();
        var (zed, ahri, _) = (await CreatePlayerAsync(organisateur, "Zed"), await CreatePlayerAsync(organisateur, "Ahri"),
            await CreatePlayerAsync(organisateur, "Absent"));
        await LierADiscordAsync(zed, "300000000000000001");
        await LierADiscordAsync(ahri, "300000000000000002");
        Factory.Bot.CandidatsVocaux = () => FakeBot.Json(
            """{"channel_id":"900000000000000009","member_ids":["300000000000000001","300000000000000002","300000000000000077"]}""");

        var candidats = await ReadJsonAsync(await organisateur.GetAsync("/soirees/open/picker/candidats-vocaux", Ct));

        Assert.Equal("900000000000000009", candidats.GetProperty("channelId").GetString());
        var joueurs = candidats.GetProperty("joueurs").EnumerateArray().ToList();
        Assert.Equal([zed, ahri], joueurs.Select(j => j.GetProperty("playerId").GetInt32()));
        Assert.Equal("Zed", joueurs[0].GetProperty("prenom").GetString());
        Assert.Equal("300000000000000001", joueurs[0].GetProperty("discordId").GetString());
        Assert.Equal(1, candidats.GetProperty("membresNonLies").GetInt32());

        var appel = Assert.Single(Factory.Bot.Requetes);
        Assert.Equal("GET", appel.Method);
        Assert.Equal($"Bearer {FakeBot.ApiKey}", appel.Authorization);
    }

    [Fact]
    public async Task Personne_en_vocal_ne_propose_aucun_joueur()
    {
        var organisateur = Factory.CreateSignedInClient();

        var candidats = await ReadJsonAsync(await organisateur.GetAsync("/soirees/open/picker/candidats-vocaux", Ct));

        Assert.Equal(System.Text.Json.JsonValueKind.Null, candidats.GetProperty("channelId").ValueKind);
        Assert.Empty(candidats.GetProperty("joueurs").EnumerateArray());
        Assert.Equal(0, candidats.GetProperty("membresNonLies").GetInt32());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Un_bot_qui_refuse_ou_n_est_pas_pret_rend_les_candidats_indisponibles(HttpStatusCode statut)
    {
        var organisateur = Factory.CreateSignedInClient();
        Factory.Bot.CandidatsVocaux = FakeBot.Statut(statut);

        await AssertRefusAsync(await organisateur.GetAsync("/soirees/open/picker/candidats-vocaux", Ct),
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Un_bot_injoignable_rend_les_candidats_indisponibles()
    {
        var organisateur = Factory.CreateSignedInClient();
        Factory.Bot.CandidatsVocaux = FakeBot.Panne();

        await AssertRefusAsync(await organisateur.GetAsync("/soirees/open/picker/candidats-vocaux", Ct),
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Les_candidats_vocaux_demandent_une_session()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync("/soirees/open/picker/candidats-vocaux", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(Factory.Bot.Requetes);
    }
}
