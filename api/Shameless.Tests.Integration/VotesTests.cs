using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Les votes MVP et DOFD d'une Game : déposés par le bot pour les dix Joueurs et les Spectateurs
/// présents en vocal, ouverts 10 minutes après la fin de la Game, un Joueur au plus par titre, jamais
/// pour soi. Les résultats (tous les Joueurs à égalité gagnent) sont lus par le récap et le bot.
/// </summary>
public class VotesTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string DiscordA1 = "100000000000000001";
    private const string DiscordA2 = "100000000000000002";
    private const string DiscordB1 = "100000000000000006";
    private const string Spectateur = "900000000000000001";

    private async Task<(CreatedSerie Serie, int GameId, HttpClient Client)> GameTermineeAsync()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);
        await LierADiscordAsync(serie.TeamA[1], DiscordA2);
        await LierADiscordAsync(serie.TeamB[0], DiscordB1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        return (serie, gameId, client);
    }

    private Task<HttpResponseMessage> VoterAsync(int gameId, string votant, int? mvp, int? dofd) =>
        Factory.CreateBotClient().PutAsJsonAsync(
            $"/bot/games/{gameId}/votes",
            new { votantDiscordId = votant, mvpPlayerId = mvp, dofdPlayerId = dofd },
            Ct);

    private Task<HttpResponseMessage> SignalerSpectateursAsync(int gameId, params string[] discordIds) =>
        Factory.CreateBotClient().PutAsJsonAsync($"/bot/games/{gameId}/spectateurs", new { discordIds }, Ct);

    private static List<int> Gagnants(JsonElement votes, string titre) =>
        votes.GetProperty(titre).GetProperty("gagnants").EnumerateArray().Select(g => g.GetInt32()).ToList();

    private static int Voix(JsonElement votes, string titre, int playerId) =>
        votes.GetProperty(titre).GetProperty("decompte").EnumerateArray()
            .Where(d => d.GetProperty("playerId").GetInt32() == playerId)
            .Select(d => d.GetProperty("voix").GetInt32())
            .SingleOrDefault();

    [Fact]
    public async Task Les_votes_des_joueurs_donnent_le_mvp_et_le_dofd_une_fois_les_votes_clos()
    {
        var (serie, gameId, client) = await GameTermineeAsync();

        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, mvp: serie.TeamA[2], dofd: serie.TeamB[3]));
        await ReadJsonAsync(await VoterAsync(gameId, DiscordB1, mvp: serie.TeamA[2], dofd: serie.TeamA[1]));
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA2, mvp: serie.TeamA[2], dofd: serie.TeamB[3]));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var votes = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/votes", Ct));

        Assert.Equal("clos", votes.GetProperty("statut").GetString());
        Assert.Equal(3, votes.GetProperty("votants").GetInt32());
        Assert.Equal([serie.TeamA[2]], Gagnants(votes, "mvp"));
        Assert.Equal([serie.TeamB[3]], Gagnants(votes, "dofd"));
        Assert.Equal(3, Voix(votes, "mvp", serie.TeamA[2]));
        Assert.Equal(2, Voix(votes, "dofd", serie.TeamB[3]));
        Assert.Equal(1, Voix(votes, "dofd", serie.TeamA[1]));
        var decompte = votes.GetProperty("dofd").GetProperty("decompte")[0];
        Assert.Equal("B4", decompte.GetProperty("prenom").GetString());
    }

    [Fact]
    public async Task Tous_les_joueurs_a_egalite_sont_gagnants()
    {
        var (serie, gameId, client) = await GameTermineeAsync();

        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, mvp: serie.TeamA[2], dofd: serie.TeamB[3]));
        await ReadJsonAsync(await VoterAsync(gameId, DiscordB1, mvp: serie.TeamB[2], dofd: serie.TeamA[4]));
        Factory.Clock.Advance(TimeSpan.FromMinutes(11));

        var votes = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/votes", Ct));

        Assert.Equal([serie.TeamA[2], serie.TeamB[2]], Gagnants(votes, "mvp").Order());
        Assert.Equal([serie.TeamA[4], serie.TeamB[3]], Gagnants(votes, "dofd").Order());
    }

    [Fact]
    public async Task Un_spectateur_present_en_vocal_peut_voter()
    {
        var (serie, gameId, _) = await GameTermineeAsync();
        var spectateurs = await ReadJsonAsync(await SignalerSpectateursAsync(gameId, Spectateur));
        Assert.Equal([Spectateur], spectateurs.GetProperty("discordIds").EnumerateArray().Select(d => d.GetString()));

        var bulletin = await ReadJsonAsync(await VoterAsync(gameId, Spectateur, mvp: serie.TeamA[0], dofd: null));

        Assert.Equal(serie.TeamA[0], bulletin.GetProperty("mvpPlayerId").GetInt32());
        Assert.Equal(JsonValueKind.Null, bulletin.GetProperty("dofdPlayerId").ValueKind);
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var votes = await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/games/{gameId}/votes", Ct));
        Assert.Equal(1, Voix(votes, "mvp", serie.TeamA[0]));
    }

    [Fact]
    public async Task Les_spectateurs_signales_remplacent_les_precedents()
    {
        var (serie, gameId, _) = await GameTermineeAsync();
        await ReadJsonAsync(await SignalerSpectateursAsync(gameId, Spectateur, "900000000000000002"));
        await ReadJsonAsync(await SignalerSpectateursAsync(gameId, "900000000000000002"));

        await AssertRefusAsync(await VoterAsync(gameId, Spectateur, mvp: serie.TeamA[0], dofd: null), HttpStatusCode.BadRequest);
        await ReadJsonAsync(await VoterAsync(gameId, "900000000000000002", mvp: serie.TeamA[0], dofd: null));
    }

    [Fact]
    public async Task Un_inconnu_ni_joueur_ni_spectateur_ne_peut_pas_voter()
    {
        var (serie, gameId, _) = await GameTermineeAsync();

        await AssertRefusAsync(await VoterAsync(gameId, Spectateur, mvp: serie.TeamA[0], dofd: null), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_joueur_ne_peut_pas_voter_pour_lui_meme()
    {
        var (serie, gameId, _) = await GameTermineeAsync();

        await AssertRefusAsync(await VoterAsync(gameId, DiscordA1, mvp: serie.TeamA[0], dofd: null), HttpStatusCode.BadRequest);
        await AssertRefusAsync(await VoterAsync(gameId, DiscordA1, mvp: null, dofd: serie.TeamA[0]), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task On_ne_vote_que_pour_un_des_dix_joueurs_de_la_game()
    {
        var (serie, gameId, client) = await GameTermineeAsync();
        var autre = await CreatePlayerAsync(client, "Autre");

        await AssertRefusAsync(await VoterAsync(gameId, DiscordA1, mvp: autre, dofd: null), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_votant_peut_changer_son_vote_ou_s_abstenir()
    {
        var (serie, gameId, client) = await GameTermineeAsync();
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, mvp: serie.TeamA[2], dofd: serie.TeamB[3]));
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, mvp: serie.TeamA[3], dofd: null));
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA2, mvp: serie.TeamA[3], dofd: serie.TeamB[3]));
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA2, mvp: null, dofd: null));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var votes = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/votes", Ct));

        Assert.Equal(1, votes.GetProperty("votants").GetInt32());
        Assert.Equal([serie.TeamA[3]], Gagnants(votes, "mvp"));
        Assert.Equal(0, Voix(votes, "mvp", serie.TeamA[2]));
        Assert.Empty(Gagnants(votes, "dofd"));
        Assert.Equal(0, votes.GetProperty("dofd").GetProperty("decompte").GetArrayLength());
    }

    [Fact]
    public async Task Les_votes_ouvrent_a_la_fin_de_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        await AssertRefusAsync(await VoterAsync(gameId, DiscordA1, mvp: serie.TeamA[1], dofd: null), HttpStatusCode.Conflict);
        var votes = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/votes", Ct));
        Assert.Equal("a_venir", votes.GetProperty("statut").GetString());
        Assert.Equal(JsonValueKind.Null, votes.GetProperty("fermetureLe").ValueKind);
    }

    [Fact]
    public async Task Les_votes_ferment_dix_minutes_apres_la_fin_de_la_game()
    {
        var (serie, gameId, client) = await GameTermineeAsync();
        Factory.Clock.Advance(TimeSpan.FromMinutes(9));
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, mvp: serie.TeamA[1], dofd: null));
        var ouverts = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/votes", Ct));
        Assert.Equal("ouverts", ouverts.GetProperty("statut").GetString());
        Assert.Equal(1, ouverts.GetProperty("votants").GetInt32());
        Assert.Empty(Gagnants(ouverts, "mvp"));
        Assert.Equal(
            ouverts.GetProperty("ouvertureLe").GetDateTime().AddMinutes(10),
            ouverts.GetProperty("fermetureLe").GetDateTime());

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        await AssertRefusAsync(await VoterAsync(gameId, DiscordA2, mvp: serie.TeamA[0], dofd: null), HttpStatusCode.Conflict);
        await AssertRefusAsync(await SignalerSpectateursAsync(gameId, Spectateur), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Le_decompte_des_voix_reste_cache_au_site_et_au_bot_tant_que_les_votes_sont_ouverts()
    {
        var (serie, gameId, client) = await GameTermineeAsync();
        await ReadJsonAsync(await VoterAsync(gameId, DiscordA1, mvp: serie.TeamA[1], dofd: serie.TeamB[2]));

        foreach (var lecteur in new[] { client, Factory.CreateBotClient() })
        {
            var prefixe = lecteur == client ? "" : "/bot";
            var votes = await ReadJsonAsync(await lecteur.GetAsync($"{prefixe}/games/{gameId}/votes", Ct));
            Assert.Equal("ouverts", votes.GetProperty("statut").GetString());
            Assert.Equal(1, votes.GetProperty("votants").GetInt32());
            Assert.Equal(0, votes.GetProperty("mvp").GetProperty("decompte").GetArrayLength());
            Assert.Equal(0, votes.GetProperty("dofd").GetProperty("decompte").GetArrayLength());
        }

        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var clos = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/votes", Ct));
        Assert.Equal(1, Voix(clos, "mvp", serie.TeamA[1]));
    }

    [Fact]
    public async Task Les_spectateurs_d_une_game_annulee_disparaissent_avec_elle()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        await ReadJsonAsync(await SignalerSpectateursAsync(gameId, Spectateur));

        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await AssertRefusAsync(await client.GetAsync($"/games/{gameId}/votes", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Les_votes_d_une_game_inconnue_repondent_404()
    {
        await AssertRefusAsync(await VoterAsync(999, DiscordA1, mvp: 1, dofd: null), HttpStatusCode.NotFound);
        await AssertRefusAsync(await SignalerSpectateursAsync(999, Spectateur), HttpStatusCode.NotFound);
        await AssertRefusAsync(await Factory.CreateSignedInClient().GetAsync("/games/999/votes", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Un_id_discord_invalide_est_refuse()
    {
        var (serie, gameId, _) = await GameTermineeAsync();

        await AssertRefusAsync(await SignalerSpectateursAsync(gameId, "pas-un-id"), HttpStatusCode.BadRequest);
        await AssertRefusAsync(await VoterAsync(gameId, "pas-un-id", mvp: serie.TeamA[0], dofd: null), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task La_fiche_d_un_joueur_compte_ses_titres_de_mvp_et_de_dofd_une_fois_les_votes_clos()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);
        await LierADiscordAsync(serie.TeamB[0], DiscordB1);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterAsync(game1, DiscordA1, mvp: serie.TeamA[2], dofd: serie.TeamB[1]));
        await ReadJsonAsync(await VoterAsync(game1, DiscordB1, mvp: serie.TeamB[2], dofd: serie.TeamB[1]));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterAsync(game2, DiscordB1, mvp: serie.TeamA[2], dofd: serie.TeamA[1]));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var game3 = await PlayAndWinAsync(client, serie.SeriesId, "B");
        await ReadJsonAsync(await VoterAsync(game3, DiscordB1, mvp: serie.TeamA[2], dofd: serie.TeamB[1]));

        async Task<JsonElement> TitresAsync(int playerId) =>
            (await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/fiche", Ct))).GetProperty("titres");

        var a3 = await TitresAsync(serie.TeamA[2]);
        Assert.Equal(2, a3.GetProperty("mvp").GetInt32());
        Assert.Equal(0, a3.GetProperty("dofd").GetInt32());
        var b2 = await TitresAsync(serie.TeamB[1]);
        Assert.Equal(0, b2.GetProperty("mvp").GetInt32());
        Assert.Equal(1, b2.GetProperty("dofd").GetInt32());
        Assert.Equal(1, (await TitresAsync(serie.TeamB[2])).GetProperty("mvp").GetInt32());
        Assert.Equal(1, (await TitresAsync(serie.TeamA[1])).GetProperty("dofd").GetInt32());
        Assert.Equal(0, (await TitresAsync(serie.TeamA[4])).GetProperty("mvp").GetInt32());
    }
}
