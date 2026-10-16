using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// La Draft de champions d'une game : l'organisateur l'ouvre, les capitaines jouent les bans et les
/// picks par leur Lien capitaine dans l'ordre Tournament, l'organisateur et les liens la lisent.
/// </summary>
public class DraftChampionsTests(ApiFactory factory) : IntegrationTest(factory)
{
    /// <summary>Le Side de chacun des vingt tours : bans B1 R1 B2 R2 B3 R3, picks B1 R1 R2 B2 B3 R3, bans R4 B4 R5 B5, picks R4 B4 B5 R5.</summary>
    private static readonly string[] SideDesTours =
    [
        "blue", "red", "blue", "red", "blue", "red",
        "blue", "red", "red", "blue", "blue", "red",
        "red", "blue", "red", "blue",
        "red", "blue", "blue", "red"
    ];

    private static readonly int[] ToursDeBan = [1, 2, 3, 4, 5, 6, 13, 14, 15, 16];

    /// <summary>Vingt champions distincts du faux catalogue, un par tour.</summary>
    private static readonly string[] Vingt =
    [
        "Ahri", "Garen", "LeeSin", "Jinx", "Thresh", "Annie", "Ashe", "Braum", "Darius", "Ezreal",
        "Fiora", "Galio", "Irelia", "Janna", "Karma", "Lulu", "Malphite", "Nami", "Orianna", "Pyke"
    ];

    private sealed record Preparee(CreatedSerie Serie, int GameId, string LienA, string LienB, string LienSpectateur)
    {
        public string LienDe(string equipe) => equipe == "A" ? LienA : LienB;
    }

    /// <summary>Une Série avec ses deux capitaines, sa game en attente au Side donné et ses liens envoyés.</summary>
    private async Task<Preparee> PreparerAsync(HttpClient client, string teamASide = "blue", int format = 3, bool fearless = false)
    {
        var serie = await CreateSerieAsync(client, format: format, fearless: fearless);
        await DesignerCapitainesAsync(client, serie);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide);
        var liens = await LiensEnvoyesAsync(client, serie.SeriesId);
        return new Preparee(serie, gameId,
            liens.GetProperty("lienCapitaineA").GetString()!,
            liens.GetProperty("lienCapitaineB").GetString()!,
            liens.GetProperty("lienSpectateur").GetString()!);
    }

    private async Task DesignerCapitainesAsync(HttpClient client, CreatedSerie serie)
    {
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/A",
            new { designation = "Manuel", playerId = serie.TeamA[0] }, Ct));
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/B",
            new { designation = "Manuel", playerId = serie.TeamB[0] }, Ct));
    }

    private async Task<JsonElement> LiensEnvoyesAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.PostAsync($"/series/{seriesId}/liens/envoyes", null, Ct));

    private async Task<HttpResponseMessage> OuvrirAsync(HttpClient client, int seriesId) =>
        await client.PostAsync($"/series/{seriesId}/draft-champions", null, Ct);

    private async Task<HttpResponseMessage> JouerAsync(string lien, int tour, string championId) =>
        await Factory.CreateAnonymousClient().PostAsJsonAsync(
            $"/liens/{Uri.EscapeDataString(lien)}/draft-champions/actions", new { tour, championId }, Ct);

    /// <summary>L'organisateur connecté joue le tour à la place de l'Équipe attendue, une fois le délai écoulé.</summary>
    private async Task<HttpResponseMessage> JouerALaPlaceAsync(HttpClient client, int gameId, int tour, string championId) =>
        await client.PostAsJsonAsync($"/games/{gameId}/draft-champions/actions", new { tour, championId }, Ct);

    private async Task<HttpResponseMessage> LireParLienAsync(string lien) =>
        await Factory.CreateAnonymousClient().GetAsync($"/liens/{Uri.EscapeDataString(lien)}/draft-champions", Ct);

    private async Task<JsonElement> LireAsync(HttpClient client, int gameId) =>
        await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/draft-champions", Ct));

    /// <summary>L'Équipe qui joue un tour, d'après le Side de l'Équipe A.</summary>
    private static string EquipeDuTour(int tour, string teamASide) =>
        SideDesTours[tour - 1] == teamASide ? "A" : "B";

    /// <summary>Joue les tours <paramref name="du"/> à <paramref name="au"/> avec le Lien capitaine attendu.</summary>
    private async Task JouerToursAsync(Preparee preparee, string teamASide, int du, int au, string[]? champions = null)
    {
        for (var tour = du; tour <= au; tour++)
            await ReadJsonAsync(await JouerAsync(preparee.LienDe(EquipeDuTour(tour, teamASide)), tour, (champions ?? Vingt)[tour - 1]));
    }

    [Fact]
    public async Task L_organisateur_ouvre_la_draft_de_champions_de_la_game_en_attente()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, teamASide: "red");

        var response = await OuvrirAsync(client, preparee.Serie.SeriesId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await ReadJsonAsync(response);
        Assert.Equal(preparee.GameId, draft.GetProperty("gameId").GetInt32());
        Assert.Equal(preparee.Serie.SeriesId, draft.GetProperty("seriesId").GetInt32());
        Assert.Equal("B", draft.GetProperty("equipeBlue").GetString());
        Assert.Equal("A", draft.GetProperty("equipeRed").GetString());
        Assert.False(draft.GetProperty("terminee").GetBoolean());
        Assert.Empty(draft.GetProperty("actions").EnumerateArray());
        var tour = draft.GetProperty("tourCourant");
        Assert.Equal(1, tour.GetProperty("numero").GetInt32());
        Assert.Equal("ban", tour.GetProperty("action").GetString());
        Assert.Equal("blue", tour.GetProperty("side").GetString());
        Assert.Equal("B", tour.GetProperty("equipe").GetString());
    }

    [Fact]
    public async Task Ouvrir_sans_game_en_attente_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await DesignerCapitainesAsync(client, serie);

        await AssertRefusAsync(await OuvrirAsync(client, serie.SeriesId), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ouvrir_sans_deux_capitaines_designes_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/A",
            new { designation = "Manuel", playerId = serie.TeamA[0] }, Ct));
        await PlayGameAsync(client, serie.SeriesId);

        await AssertRefusAsync(await OuvrirAsync(client, serie.SeriesId), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Une_seule_draft_de_champions_par_game()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        await AssertRefusAsync(await OuvrirAsync(client, preparee.Serie.SeriesId), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ouvrir_la_draft_d_une_serie_inconnue_repond_404()
    {
        var client = Factory.CreateSignedInClient();

        await AssertRefusAsync(await OuvrirAsync(client, 999), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task L_etat_donne_les_vingt_tours_de_l_ordre_tournament_et_l_equipe_attendue_a_chacun()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, teamASide: "red");

        var draft = await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        var tours = draft.GetProperty("tours").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, 20), tours.Select(t => t.GetProperty("numero").GetInt32()));
        Assert.Equal(SideDesTours, tours.Select(t => t.GetProperty("side").GetString()));
        Assert.Equal(SideDesTours.Select(s => s == "red" ? "A" : "B"), tours.Select(t => t.GetProperty("equipe").GetString()));
        Assert.Equal(Enumerable.Range(1, 20).Select(t => ToursDeBan.Contains(t) ? "ban" : "pick"),
            tours.Select(t => t.GetProperty("action").GetString()));
    }

    [Fact]
    public async Task Les_capitaines_jouent_les_vingt_tours_dans_l_ordre_tournament_selon_le_side()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, teamASide: "red");
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        await JouerToursAsync(preparee, "red", 1, 20);

        var draft = await LireAsync(client, preparee.GameId);
        Assert.True(draft.GetProperty("terminee").GetBoolean());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("tourCourant").ValueKind);
        var actions = draft.GetProperty("actions").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, 20), actions.Select(a => a.GetProperty("numero").GetInt32()));
        Assert.Equal(Vingt, actions.Select(a => a.GetProperty("championId").GetString()));
        Assert.Equal(SideDesTours, actions.Select(a => a.GetProperty("side").GetString()));
        Assert.Equal(SideDesTours.Select(s => s == "red" ? "A" : "B"), actions.Select(a => a.GetProperty("equipe").GetString()));
        Assert.Equal(Enumerable.Range(1, 20).Select(t => ToursDeBan.Contains(t) ? "ban" : "pick"),
            actions.Select(a => a.GetProperty("action").GetString()));
        foreach (var equipe in new[] { "A", "B" })
        {
            Assert.Equal(5, actions.Count(a => a.GetProperty("equipe").GetString() == equipe && a.GetProperty("action").GetString() == "ban"));
            Assert.Equal(5, actions.Count(a => a.GetProperty("equipe").GetString() == equipe && a.GetProperty("action").GetString() == "pick"));
        }
    }

    [Fact]
    public async Task Les_picks_sont_des_champions_de_l_equipe_sans_joueur_associe()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        await JouerToursAsync(preparee, "blue", 1, 20);

        var draft = await LireAsync(client, preparee.GameId);
        foreach (var action in draft.GetProperty("actions").EnumerateArray())
            Assert.Equal(["numero", "action", "side", "equipe", "championId"], action.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Un_tour_deja_joue_ou_hors_tour_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        await JouerToursAsync(preparee, "blue", 1, 1);

        await AssertRefusAsync(await JouerAsync(preparee.LienA, 1, "Garen"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await JouerAsync(preparee.LienA, 3, "Garen"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await JouerAsync(preparee.LienB, 3, "Garen"), HttpStatusCode.Conflict);

        var draft = await LireAsync(client, preparee.GameId);
        Assert.Single(draft.GetProperty("actions").EnumerateArray());
        Assert.Equal(2, draft.GetProperty("tourCourant").GetProperty("numero").GetInt32());
    }

    [Fact]
    public async Task Un_capitaine_ne_joue_pas_le_tour_de_l_autre_equipe()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        // Tour 1 : ban du côté blue, l'Équipe A.
        await AssertRefusAsync(await JouerAsync(preparee.LienB, 1, "Ahri"), HttpStatusCode.Conflict);

        Assert.Empty((await LireAsync(client, preparee.GameId)).GetProperty("actions").EnumerateArray());
    }

    [Fact]
    public async Task Un_champion_inconnu_est_refuse_en_400()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        await AssertRefusAsync(await JouerAsync(preparee.LienA, 1, "PasUnChampion"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_champion_deja_banni_ou_deja_pick_dans_la_game_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        await JouerToursAsync(preparee, "blue", 1, 7);

        // Tour 8 : pick du côté red (Équipe B). Ahri est bannie au tour 1, Ashe pick au tour 7.
        await AssertRefusAsync(await JouerAsync(preparee.LienB, 8, "Ahri"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await JouerAsync(preparee.LienB, 8, "Ashe"), HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, (await JouerAsync(preparee.LienB, 8, "Braum")).StatusCode);
    }

    [Fact]
    public async Task Le_lien_spectateur_ne_permet_aucune_action()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        await AssertRefusAsync(await JouerAsync(preparee.LienSpectateur, 1, "Ahri"), HttpStatusCode.Forbidden);

        Assert.Empty((await LireAsync(client, preparee.GameId)).GetProperty("actions").EnumerateArray());
    }

    [Fact]
    public async Task Un_lien_capitaine_renouvele_ne_permet_plus_de_jouer()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        var renouveles = await ReadJsonAsync(await client.PostAsync($"/series/{preparee.Serie.SeriesId}/liens/A/renouveler", null, Ct));

        await AssertRefusAsync(await JouerAsync(preparee.LienA, 1, "Ahri"), HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await JouerAsync(renouveles.GetProperty("lienCapitaineA").GetString()!, 1, "Ahri")).StatusCode);
    }

    [Fact]
    public async Task Un_lien_capitaine_n_agit_que_sur_la_draft_de_sa_serie()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        // Une deuxième Série, sans Draft de champions ouverte.
        var ids = await CreatePlayersAsync(client, "C1", "C2", "C3", "C4", "C5", "D1", "D2", "D3", "D4", "D5");
        var autre = await ReadJsonAsync(await client.PostAsJsonAsync("/series", new { teamA = ids[..5], teamB = ids[5..], format = 1 }, Ct));
        var liensAutre = await ReadJsonAsync(await client.GetAsync($"/series/{autre.GetProperty("seriesId").GetInt32()}/liens", Ct));

        await AssertRefusAsync(await JouerAsync(liensAutre.GetProperty("lienCapitaineA").GetString()!, 1, "Ahri"), HttpStatusCode.NotFound);

        Assert.Empty((await LireAsync(client, preparee.GameId)).GetProperty("actions").EnumerateArray());
    }

    [Fact]
    public async Task Les_capitaines_et_le_spectateur_lisent_la_draft_en_cours_sans_connexion()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        await JouerToursAsync(preparee, "blue", 1, 3);

        foreach (var lien in new[] { preparee.LienA, preparee.LienB, preparee.LienSpectateur })
        {
            var draft = await ReadJsonAsync(await LireParLienAsync(lien));
            Assert.Equal(preparee.GameId, draft.GetProperty("gameId").GetInt32());
            Assert.Equal(3, draft.GetProperty("actions").GetArrayLength());
            Assert.Equal(4, draft.GetProperty("tourCourant").GetProperty("numero").GetInt32());
            Assert.Equal("B", draft.GetProperty("tourCourant").GetProperty("equipe").GetString());
        }
    }

    [Fact]
    public async Task Aucune_lecture_de_la_draft_n_expose_de_lien()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        await JouerToursAsync(preparee, "blue", 1, 2);

        var corps = new List<string>
        {
            await (await client.GetAsync($"/games/{preparee.GameId}/draft-champions", Ct)).Content.ReadAsStringAsync(Ct),
            await (await JouerAsync(preparee.LienA, 3, "LeeSin")).Content.ReadAsStringAsync(Ct)
        };
        foreach (var lien in new[] { preparee.LienA, preparee.LienB, preparee.LienSpectateur })
            corps.Add(await (await LireParLienAsync(lien)).Content.ReadAsStringAsync(Ct));

        Assert.All(corps, c =>
        {
            Assert.Contains("\"gameId\"", c);
            Assert.DoesNotContain(preparee.LienA, c);
            Assert.DoesNotContain(preparee.LienB, c);
            Assert.DoesNotContain(preparee.LienSpectateur, c);
        });
    }

    /// <summary>
    /// Une Série validée par le Picker avec l'Autorole, ses deux capitaines, sa game en attente côté
    /// blue pour l'Équipe A, ses liens envoyés et sa Draft de champions ouverte.
    /// </summary>
    private async Task<Preparee> PreparerAvecAutoroleAsync(HttpClient client)
    {
        await OpenSoireeAsync(client);
        var dix = await CreatePlayersAsync(client, "J1", "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10");
        await ReadJsonAsync(await client.PutAsJsonAsync("/soirees/open/picker/selection", new { playerIds = dix }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/roulette", new { variante = "Aleatoire" }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/autorole", new { variante = "Aleatoire" }, Ct));
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/validate", new { format = 3 }, Ct));
        var seriesId = created.GetProperty("seriesId").GetInt32();
        var serieLue = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        var serie = new CreatedSerie(seriesId,
            serieLue.GetProperty("teamA").EnumerateArray().Select(j => j.GetProperty("playerId").GetInt32()).ToArray(),
            serieLue.GetProperty("teamB").EnumerateArray().Select(j => j.GetProperty("playerId").GetInt32()).ToArray());
        await DesignerCapitainesAsync(client, serie);
        var gameId = await PlayGameAsync(client, seriesId);
        var liens = await LiensEnvoyesAsync(client, seriesId);
        var preparee = new Preparee(serie, gameId, liens.GetProperty("lienCapitaineA").GetString()!,
            liens.GetProperty("lienCapitaineB").GetString()!, liens.GetProperty("lienSpectateur").GetString()!);
        await ReadJsonAsync(await OuvrirAsync(client, seriesId));
        return preparee;
    }

    [Fact]
    public async Task Les_roles_autorole_sont_caches_pendant_la_draft_puis_reveles_a_la_fin()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAvecAutoroleAsync(client);
        var (seriesId, gameId) = (preparee.Serie.SeriesId, preparee.GameId);
        var serieLue = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        await JouerToursAsync(preparee, "blue", 1, 19);

        var lectures = await LecturesAsync(preparee.LienA);
        Assert.All(lectures, draft => Assert.All(Joueurs(draft), j => Assert.Equal(JsonValueKind.Null, j.GetProperty("role").ValueKind)));

        await JouerToursAsync(preparee, "blue", 20, 20);

        lectures = await LecturesAsync(preparee.LienB);
        Assert.All(lectures, draft =>
        {
            Assert.Equal(["Adc", "Jungle", "Mid", "Support", "Top"], draft.GetProperty("teamA").EnumerateArray().Select(j => j.GetProperty("role").GetString()).Order());
            Assert.Equal(["Adc", "Jungle", "Mid", "Support", "Top"], draft.GetProperty("teamB").EnumerateArray().Select(j => j.GetProperty("role").GetString()).Order());
        });

        // La Draft de champions, par l'organisateur et par les liens, et la Série elle-même (seule, en
        // cours, dans sa Soirée) : toutes les lectures qui portent les rôles de l'Autorole.
        async Task<JsonElement[]> LecturesAsync(string lienCapitaine)
        {
            var soireeId = serieLue.GetProperty("soireeId").GetInt32();
            return
            [
                await LireAsync(client, gameId),
                await ReadJsonAsync(await LireParLienAsync(lienCapitaine)),
                await ReadJsonAsync(await LireParLienAsync(preparee.LienSpectateur)),
                await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct)),
                (await ReadJsonAsync(await client.GetAsync("/series/active", Ct))).GetProperty("series")[0],
                (await ReadJsonAsync(await client.GetAsync($"/soirees/{soireeId}/series", Ct))).GetProperty("series")[0]
            ];
        }

        static IEnumerable<JsonElement> Joueurs(JsonElement draft) =>
            draft.GetProperty("teamA").EnumerateArray().Concat(draft.GetProperty("teamB").EnumerateArray());
    }

    [Fact]
    public async Task Saisir_le_resultat_pendant_la_draft_la_termine_et_revele_les_roles()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAvecAutoroleAsync(client);
        await JouerToursAsync(preparee, "blue", 1, 5);

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{preparee.GameId}/result", new { winner = "A" }, Ct));

        var draft = await LireAsync(client, preparee.GameId);
        Assert.True(draft.GetProperty("terminee").GetBoolean());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("tourCourant").ValueKind);
        Assert.Equal(5, draft.GetProperty("actions").GetArrayLength());
        Assert.All(draft.GetProperty("teamA").EnumerateArray(), j => Assert.NotEqual(JsonValueKind.Null, j.GetProperty("role").ValueKind));
        var serie = await ReadJsonAsync(await client.GetAsync($"/series/{preparee.Serie.SeriesId}", Ct));
        Assert.All(serie.GetProperty("teamB").EnumerateArray(), j => Assert.NotEqual(JsonValueKind.Null, j.GetProperty("role").ValueKind));

        await AssertRefusAsync(await JouerAsync(preparee.LienB, 6, "Annie"), HttpStatusCode.Conflict);
        Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 6, "Annie"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task La_draft_de_la_game_suivante_s_ouvre_depuis_la_serie_et_fearless_ne_bloque_pas_un_champion_deja_joue()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, fearless: true);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        await JouerToursAsync(preparee, "blue", 1, 20);
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{preparee.GameId}/result", new { winner = "A" }, Ct));

        var suivante = await PlayGameAsync(client, preparee.Serie.SeriesId, "red");
        var draft = await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        Assert.Equal(suivante, draft.GetProperty("gameId").GetInt32());
        Assert.Equal("B", draft.GetProperty("tourCourant").GetProperty("equipe").GetString());
        // Mêmes champions qu'à la game précédente (Ahri bannie, Ashe pick) : rien n'est bloqué.
        await JouerToursAsync(preparee, "red", 1, 20);
        var lue = await ReadJsonAsync(await LireParLienAsync(preparee.LienSpectateur));
        Assert.Equal(suivante, lue.GetProperty("gameId").GetInt32());
        Assert.Equal(Vingt, lue.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("championId").GetString()));
        Assert.Equal(20, (await LireAsync(client, preparee.GameId)).GetProperty("actions").GetArrayLength());
    }

    [Fact]
    public async Task La_draft_terminee_reste_lisible_sur_la_game_apres_la_fin_de_la_serie()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, format: 1);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        await JouerToursAsync(preparee, "blue", 1, 20);
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{preparee.GameId}/result", new { winner = "B" }, Ct));
        Assert.Equal("Gagnee", (await ReadJsonAsync(await client.GetAsync($"/series/{preparee.Serie.SeriesId}", Ct))).GetProperty("outcome").GetString());

        var draft = await LireAsync(client, preparee.GameId);
        var parLien = await ReadJsonAsync(await LireParLienAsync(preparee.LienSpectateur));

        Assert.True(draft.GetProperty("terminee").GetBoolean());
        Assert.Equal(Vingt, draft.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("championId").GetString()));
        Assert.Equal(20, parLien.GetProperty("actions").GetArrayLength());
    }

    [Fact]
    public async Task Une_game_sans_draft_de_champions_repond_404()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);

        await AssertRefusAsync(await client.GetAsync($"/games/{preparee.GameId}/draft-champions", Ct), HttpStatusCode.NotFound);
        await AssertRefusAsync(await LireParLienAsync(preparee.LienA), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task L_etat_garde_l_heure_de_debut_du_tour_courant_selon_l_horloge()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        var maintenant = Factory.Clock.GetUtcNow();
        var debut = new DateTimeOffset(maintenant.Ticks - maintenant.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero).AddMinutes(1);
        Factory.Clock.SetUtcNow(debut);

        var ouverte = await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        Factory.Clock.Advance(TimeSpan.FromSeconds(42));
        var apres = await ReadJsonAsync(await JouerAsync(preparee.LienA, 1, "Ahri"));

        Assert.Equal(debut.UtcDateTime, ouverte.GetProperty("tourCourant").GetProperty("commenceLe").GetDateTime());
        Assert.Equal(debut.AddSeconds(42).UtcDateTime, apres.GetProperty("tourCourant").GetProperty("commenceLe").GetDateTime());
    }

    [Fact]
    public async Task Des_actions_simultanees_sur_le_meme_tour_n_en_enregistrent_qu_une()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        var responses = await Task.WhenAll(Vingt[..8].Select(champion => JouerAsync(preparee.LienA, 1, champion)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Single((await LireAsync(client, preparee.GameId)).GetProperty("actions").EnumerateArray());
    }

    [Fact]
    public async Task La_draft_de_champions_est_publiee_dans_openapi()
    {
        var client = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var document = await ReadJsonAsync(await client.GetAsync("/openapi/v1.json", Ct));

        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/series/{id}/draft-champions", out _));
        Assert.True(paths.TryGetProperty("/games/{gameId}/draft-champions", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions", out _));
        Assert.True(paths.TryGetProperty("/liens/{lien}/draft-champions/actions", out _));
        Assert.True(paths.TryGetProperty("/games/{gameId}/draft-champions/actions", out _));
    }

    /// <summary>L'horloge calée sur une seconde ronde, pour comparer les dates restituées.</summary>
    private DateTimeOffset CalerHorloge()
    {
        var maintenant = Factory.Clock.GetUtcNow();
        var debut = new DateTimeOffset(maintenant.Ticks - maintenant.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero).AddMinutes(1);
        Factory.Clock.SetUtcNow(debut);
        return debut;
    }

    [Fact]
    public async Task L_etat_restitue_l_echeance_du_tour_courant_soixante_secondes_apres_son_debut()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        var debut = CalerHorloge();

        var ouverte = await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        Factory.Clock.Advance(TimeSpan.FromSeconds(42));
        var apres = await ReadJsonAsync(await JouerAsync(preparee.LienA, 1, "Ahri"));
        var parLien = await ReadJsonAsync(await LireParLienAsync(preparee.LienSpectateur));

        Assert.Equal(debut.AddSeconds(60).UtcDateTime, ouverte.GetProperty("tourCourant").GetProperty("echeance").GetDateTime());
        Assert.Equal(debut.AddSeconds(102).UtcDateTime, apres.GetProperty("tourCourant").GetProperty("echeance").GetDateTime());
        Assert.Equal(debut.AddSeconds(102).UtcDateTime, parLien.GetProperty("tourCourant").GetProperty("echeance").GetDateTime());
    }

    [Fact]
    public async Task Les_dates_lues_en_base_sont_restituees_en_utc()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        var ouverte = await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));

        var lue = await LireAsync(client, preparee.GameId);
        var parLien = await ReadJsonAsync(await LireParLienAsync(preparee.LienSpectateur));

        var envoyee = ouverte.GetProperty("tourCourant").GetProperty("echeance").GetString();
        Assert.EndsWith("Z", envoyee);
        Assert.Equal(envoyee, lue.GetProperty("tourCourant").GetProperty("echeance").GetString());
        Assert.Equal(envoyee, parLien.GetProperty("tourCourant").GetProperty("echeance").GetString());
        Assert.EndsWith("Z", parLien.GetProperty("tourCourant").GetProperty("commenceLe").GetString());
    }

    [Fact]
    public async Task Avant_l_echeance_l_organisateur_ne_joue_pas_a_la_place_d_un_capitaine()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        Factory.Clock.Advance(TimeSpan.FromSeconds(59));

        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 1, "Ahri"), HttpStatusCode.Conflict);

        Assert.Empty((await LireAsync(client, preparee.GameId)).GetProperty("actions").EnumerateArray());
    }

    [Fact]
    public async Task Apres_l_echeance_l_organisateur_joue_l_action_de_l_equipe_attendue()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client, teamASide: "red");
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        Factory.Clock.Advance(TimeSpan.FromSeconds(61));

        var response = await JouerALaPlaceAsync(client, preparee.GameId, 1, "Ahri");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = await ReadJsonAsync(response);
        var action = Assert.Single(draft.GetProperty("actions").EnumerateArray());
        Assert.Equal(1, action.GetProperty("numero").GetInt32());
        Assert.Equal("B", action.GetProperty("equipe").GetString());
        Assert.Equal("Ahri", action.GetProperty("championId").GetString());
        Assert.Equal(2, draft.GetProperty("tourCourant").GetProperty("numero").GetInt32());
        Assert.Equal("A", draft.GetProperty("tourCourant").GetProperty("equipe").GetString());
    }

    [Fact]
    public async Task Le_delai_repart_a_chaque_tour()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await ReadJsonAsync(await JouerALaPlaceAsync(client, preparee.GameId, 1, "Ahri"));

        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 2, "Garen"), HttpStatusCode.Conflict);

        Factory.Clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(HttpStatusCode.OK, (await JouerALaPlaceAsync(client, preparee.GameId, 2, "Garen")).StatusCode);
    }

    [Fact]
    public async Task Apres_l_echeance_le_capitaine_peut_toujours_jouer_tant_que_le_tour_n_a_pas_change()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        Factory.Clock.Advance(TimeSpan.FromSeconds(90));

        Assert.Equal(HttpStatusCode.OK, (await JouerAsync(preparee.LienA, 1, "Ahri")).StatusCode);

        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 1, "Garen"), HttpStatusCode.Conflict);
        var draft = await LireAsync(client, preparee.GameId);
        Assert.Equal("Ahri", Assert.Single(draft.GetProperty("actions").EnumerateArray()).GetProperty("championId").GetString());
    }

    [Fact]
    public async Task Le_capitaine_ne_rejoue_pas_un_tour_deja_joue_par_l_organisateur()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await ReadJsonAsync(await JouerALaPlaceAsync(client, preparee.GameId, 1, "Ahri"));

        await AssertRefusAsync(await JouerAsync(preparee.LienA, 1, "Garen"), HttpStatusCode.Conflict);

        Assert.Single((await LireAsync(client, preparee.GameId)).GetProperty("actions").EnumerateArray());
    }

    [Fact]
    public async Task L_organisateur_ne_joue_ni_hors_tour_ni_un_champion_deja_utilise_ni_une_draft_terminee()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        await JouerToursAsync(preparee, "blue", 1, 1);
        Factory.Clock.Advance(TimeSpan.FromSeconds(61));

        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 3, "Garen"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 2, "Ahri"), HttpStatusCode.Conflict);
        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 2, "Inconnu"), HttpStatusCode.BadRequest);

        await JouerToursAsync(preparee, "blue", 2, 20);
        Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 20, "Zed"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Sans_catalogue_des_champions_les_actions_de_la_draft_repondent_503()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);
        await ReadJsonAsync(await OuvrirAsync(client, preparee.Serie.SeriesId));
        Factory.Champions.Indisponible = true;

        await AssertRefusAsync(await JouerAsync(preparee.LienA, 1, "Ahri"), HttpStatusCode.ServiceUnavailable);
        Factory.Clock.Advance(TimeSpan.FromSeconds(61));
        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 1, "Ahri"), HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Jouer_a_la_place_sur_une_game_sans_draft_repond_404()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await PreparerAsync(client);

        await AssertRefusAsync(await JouerALaPlaceAsync(client, preparee.GameId, 1, "Ahri"), HttpStatusCode.NotFound);
    }
}
