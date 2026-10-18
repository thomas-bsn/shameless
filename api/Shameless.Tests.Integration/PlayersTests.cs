using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shameless.Application.Interfaces;
using Shameless.Application.Joueurs;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class PlayersTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task A_created_player_appears_in_the_player_list_with_their_riot_rank()
    {
        Factory.Riot.AddAccount("Faker#KR1", "CHALLENGER", "I", lp: 1200);
        var client = Factory.CreateSignedInClient();

        var created = await client.PostAsJsonAsync("/players", new { prenom = "Lee", riotId = "Faker#KR1" }, Ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var players = await client.GetFromJsonAsync<JsonElement>("/players", Ct);
        var player = Assert.Single(players.EnumerateArray());
        Assert.Equal("Lee", player.GetProperty("prenom").GetString());
        Assert.Equal("Faker#KR1", player.GetProperty("riotId").GetString());
        Assert.Equal("CHALLENGER", player.GetProperty("rankTier").GetString());
        Assert.Equal(JsonValueKind.Null, player.GetProperty("rankDivision").ValueKind);
        Assert.Equal(1200, player.GetProperty("lp").GetInt32());
    }

    [Fact]
    public async Task A_signed_in_user_can_link_their_discord_account_to_a_player()
    {
        var client = Factory.CreateSignedInClient(discordId: "2001");
        var playerId = await CreatePlayerAsync(client, "Alice");

        var notLinked = await client.GetAsync("/players/me", Ct);
        Assert.Equal(HttpStatusCode.NotFound, notLinked.StatusCode);

        var link = await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));
        Assert.True(link.GetProperty("success").GetBoolean());

        var me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
        Assert.Equal(playerId, me.GetProperty("id").GetInt32());
        Assert.Equal("Alice", me.GetProperty("prenom").GetString());
    }

    [Fact]
    public async Task A_linked_player_can_set_and_delete_their_own_peak()
    {
        var client = Factory.CreateSignedInClient(discordId: "2002");
        var playerId = await CreatePlayerAsync(client, "Bob");
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));

        await ReadJsonAsync(await client.PostAsJsonAsync("/players/peak",
            new { peakTier = "DIAMOND", peakDivision = 2, peakSeason = "S24", peakLp = 40 }, Ct));

        var me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
        Assert.Equal("DIAMOND", me.GetProperty("peakTier").GetString());
        Assert.Equal(2, me.GetProperty("peakDivision").GetInt32());
        Assert.Equal("S24", me.GetProperty("peakSeason").GetString());
        Assert.Equal(40, me.GetProperty("peakLp").GetInt32());

        await ReadJsonAsync(await client.DeleteAsync("/players/peak", Ct));

        me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
        Assert.Equal(JsonValueKind.Null, me.GetProperty("peakTier").ValueKind);
        Assert.Equal(0, me.GetProperty("peakLp").GetInt32());
    }

    private string SaisonCourante => SaisonsDePeak.Courante(Factory.Clock.GetUtcNow().Year);

    [Fact]
    public async Task Un_joueur_refuse_une_saison_de_peak_hors_de_S24_a_la_saison_courante()
    {
        var client = Factory.CreateSignedInClient(discordId: "2010");
        var playerId = await CreatePlayerAsync(client, "Dora");
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));
        var anneeSuivante = $"S{(Factory.Clock.GetUtcNow().Year + 1) % 100}";

        foreach (var saison in new[] { "S23", anneeSuivante, "S2025", "saison", null })
        {
            var reponse = await client.PostAsJsonAsync("/players/peak",
                new { peakTier = "GOLD", peakDivision = 1, peakSeason = saison, peakLp = 0 }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
        }

        var me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
        Assert.Equal(JsonValueKind.Null, me.GetProperty("peakTier").ValueKind);
    }

    [Fact]
    public async Task Un_joueur_accepte_les_saisons_de_S24_a_la_saison_courante()
    {
        var client = Factory.CreateSignedInClient(discordId: "2011");
        var playerId = await CreatePlayerAsync(client, "Eli");
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));

        foreach (var saison in new[] { "S24", SaisonCourante })
        {
            var reponse = await client.PostAsJsonAsync("/players/peak",
                new { peakTier = "PLATINUM", peakDivision = 3, peakSeason = saison, peakLp = 0 }, Ct);
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
            var me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
            Assert.Equal(saison, me.GetProperty("peakSeason").GetString());
        }
    }

    [Fact]
    public async Task Le_rang_du_peak_n_est_pas_verifie()
    {
        var client = Factory.CreateSignedInClient(discordId: "2012");
        var playerId = await CreatePlayerAsync(client, "Flo"); // GOLD II
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));

        var reponse = await client.PostAsJsonAsync("/players/peak",
            new { peakTier = "CHALLENGER", peakDivision = (int?)null, peakSeason = "S24", peakLp = 2000 }, Ct);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
    }

    [Fact]
    public async Task Les_saisons_proposees_sont_exposees_a_tout_utilisateur_connecte()
    {
        var client = Factory.CreateSignedInClient(discordId: "2013");

        var saisons = await ReadJsonAsync(await client.GetAsync("/players/peak/saisons", Ct));

        Assert.Equal(SaisonCourante, saisons.GetProperty("courante").GetString());
        var liste = saisons.GetProperty("saisons").EnumerateArray().Select(s => s.GetString()).ToList();
        Assert.Equal("S24", liste.First());
        Assert.Equal(SaisonCourante, liste.Last());
    }

    [Fact]
    public async Task Un_admin_refuse_aussi_une_saison_invalide_pour_un_autre_joueur()
    {
        var admin = Factory.CreateAdminClient();
        var playerId = await CreatePlayerAsync(admin, "Gus");

        var reponse = await admin.PostAsJsonAsync($"/players/{playerId}/peak",
            new { peakTier = "GOLD", peakDivision = 1, peakSeason = "S10", peakLp = 0 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);
    }

    [Fact]
    public async Task The_cost_starts_from_the_current_soloq_rank_not_the_peak()
    {
        var client = Factory.CreateSignedInClient(discordId: "2003");
        var playerId = await CreatePlayerAsync(client, "Chloe"); // GOLD II
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/peak",
            new { peakTier = "DIAMOND", peakDivision = 2, peakSeason = "S24", peakLp = 0 }, Ct));

        var cost = await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost", Ct));

        Assert.Equal("Chloe", cost.GetProperty("name").GetString());
        Assert.Equal(2m, cost.GetProperty("cost").GetDecimal());
        Assert.Equal("C", cost.GetProperty("tier").GetString());
        Assert.Equal("RangSoloQ", cost.GetProperty("sourceDuCostDeDepart").GetString());
        Assert.Equal(0, cost.GetProperty("gamesTerminees").GetInt32());
        Assert.Equal(5, cost.GetProperty("gamesMinimum").GetInt32());
    }

    [Fact]
    public async Task A_player_can_be_read_by_id()
    {
        Factory.Riot.AddAccount("Caps#EUW", "MASTER", "I", lp: 300, mainRole: "MIDDLE");
        var client = Factory.CreateSignedInClient();
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/players", new { prenom = "Rasmus", riotId = "Caps#EUW" }, Ct));

        var player = await ReadJsonAsync(await client.GetAsync($"/players/{created.GetProperty("id").GetInt32()}", Ct));

        Assert.Equal("Rasmus", player.GetProperty("prenom").GetString());
        Assert.Equal("Caps#EUW", player.GetProperty("riotId").GetString());
        Assert.Equal("MASTER", player.GetProperty("rankTier").GetString());
        Assert.Equal(300, player.GetProperty("lp").GetInt32());
        Assert.Equal("MIDDLE", player.GetProperty("mainRole").GetString());
        Assert.True(player.TryGetProperty("currentCost", out _));
    }

    [Fact]
    public async Task The_riot_preview_shows_the_account_rank_and_role_without_saving_a_player()
    {
        Factory.Riot.AddAccount("Faker#KR1", "CHALLENGER", "I", lp: 1200, mainRole: "MIDDLE");
        var client = Factory.CreateSignedInClient();

        var preview = await ReadJsonAsync(await client.GetAsync("/players/riot-preview?riotId=Faker%23KR1", Ct));

        Assert.Equal("Faker#KR1", preview.GetProperty("riotId").GetString());
        Assert.Equal("CHALLENGER", preview.GetProperty("rankTier").GetString());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("rankDivision").ValueKind);
        Assert.Equal(1200, preview.GetProperty("lp").GetInt32());
        Assert.Equal("MIDDLE", preview.GetProperty("mainRole").GetString());
        Assert.False(preview.GetProperty("alreadyRegistered").GetBoolean());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/players", Ct)).EnumerateArray());
    }

    [Fact]
    public async Task Le_rafraichissement_des_rangs_efface_le_rang_d_une_file_ou_le_joueur_n_est_plus_classe()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Riot.AddAccount("Duo#EUW", "GOLD", "II", lp: 40, flexTier: "SILVER", flexDivision: "I", flexLp: 10);
        var playerId = (await ReadJsonAsync(await client.PostAsJsonAsync("/players", new { prenom = "Duo", riotId = "Duo#EUW" }, Ct)))
            .GetProperty("id").GetInt32();

        // Riot répond : le joueur est toujours classé en SoloQ, plus en Flex.
        Factory.Riot.AddAccount("Duo#EUW", "PLATINUM", "IV", lp: 5);
        await RafraichirLesRangsAsync();

        var player = await ReadJsonAsync(await client.GetAsync($"/players/{playerId}", Ct));
        Assert.Equal(("PLATINUM", 4), (player.GetProperty("rankTier").GetString(), player.GetProperty("rankDivision").GetInt32()));
        Assert.Equal(JsonValueKind.Null, player.GetProperty("flexRankTier").ValueKind);
        Assert.Equal(JsonValueKind.Null, player.GetProperty("flexRankDivision").ValueKind);
        Assert.Equal(JsonValueKind.Null, player.GetProperty("flexLp").ValueKind);
    }

    [Fact]
    public async Task Sans_reponse_de_riot_le_rafraichissement_garde_les_derniers_rangs_connus()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Riot.AddAccount("Duo#EUW", "GOLD", "II", lp: 40, flexTier: "SILVER", flexDivision: "I", flexLp: 10);
        var playerId = (await ReadJsonAsync(await client.PostAsJsonAsync("/players", new { prenom = "Duo", riotId = "Duo#EUW" }, Ct)))
            .GetProperty("id").GetInt32();

        Factory.Riot.RangsIndisponibles = true;
        await RafraichirLesRangsAsync();

        var player = await ReadJsonAsync(await client.GetAsync($"/players/{playerId}", Ct));
        Assert.Equal("GOLD", player.GetProperty("rankTier").GetString());
        Assert.Equal("SILVER", player.GetProperty("flexRankTier").GetString());
    }

    private async Task RafraichirLesRangsAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IPlayerService>().RefreshAllRanksAsync();
    }

    [Fact]
    public async Task The_riot_preview_flags_an_account_that_is_already_a_player()
    {
        var client = Factory.CreateSignedInClient();
        await CreatePlayerAsync(client, "Deja", "GOLD", "III");

        var preview = await ReadJsonAsync(await client.GetAsync("/players/riot-preview?riotId=Deja%23EUW", Ct));

        Assert.Equal("GOLD", preview.GetProperty("rankTier").GetString());
        Assert.Equal(3, preview.GetProperty("rankDivision").GetInt32());
        Assert.True(preview.GetProperty("alreadyRegistered").GetBoolean());
    }
}
