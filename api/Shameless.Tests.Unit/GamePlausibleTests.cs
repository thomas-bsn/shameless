using Shameless.Application.Clips;

namespace Shameless.Tests.Unit;

/// <summary>
/// La Game plausible d'un Clip (ADR 0014) : une Game lancée le jour de publication du clip, dans le
/// fuseau de l'Instance, et avant sa publication. Le clip n'est rattaché que s'il n'y en a qu'une.
/// </summary>
public class GamePlausibleTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    private static DateTime Utc(int mois, int jour, int heure, int minute = 0) =>
        new(2026, mois, jour, heure, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Le_jour_de_publication_commence_a_minuit_heure_de_l_instance()
    {
        // 17 octobre 2026, 21 h UTC = 23 h à Paris (heure d'été, UTC+2) : le jour commence à 22 h UTC la veille.
        var jour = GamePlausible.JourDePublication(Utc(10, 17, 21), Paris);

        Assert.Equal(Utc(10, 16, 22), jour.Debut);
        Assert.Equal(Utc(10, 17, 22), jour.Fin);
    }

    [Fact]
    public void Apres_minuit_heure_de_l_instance_c_est_deja_le_jour_suivant()
    {
        // 22 h 30 UTC = 0 h 30 à Paris le 18 octobre.
        var jour = GamePlausible.JourDePublication(Utc(10, 17, 22, 30), Paris);

        Assert.Equal(Utc(10, 17, 22), jour.Debut);
    }

    [Fact]
    public void Le_jour_du_changement_d_heure_dure_vingt_cinq_heures()
    {
        // Passage à l'heure d'hiver à Paris le 25 octobre 2026.
        var jour = GamePlausible.JourDePublication(Utc(10, 25, 12), Paris);

        Assert.Equal(Utc(10, 24, 22), jour.Debut);
        Assert.Equal(Utc(10, 25, 23), jour.Fin);
    }

    [Fact]
    public void Une_seule_game_lancee_le_jour_avant_la_publication_est_la_game_plausible()
    {
        var publieLe = Utc(10, 17, 21);

        var game = GamePlausible.Unique(publieLe, Paris, [(7, Utc(10, 17, 19))]);

        Assert.Equal(7, game);
    }

    [Fact]
    public void Deux_games_lancees_le_jour_avant_la_publication_sont_ambigues()
    {
        var game = GamePlausible.Unique(Utc(10, 17, 21), Paris, [(7, Utc(10, 17, 19)), (8, Utc(10, 17, 20))]);

        Assert.Null(game);
    }

    [Fact]
    public void Une_game_lancee_apres_la_publication_n_est_pas_plausible()
    {
        var game = GamePlausible.Unique(Utc(10, 17, 19, 30), Paris, [(7, Utc(10, 17, 19)), (8, Utc(10, 17, 20))]);

        Assert.Equal(7, game);
    }

    [Fact]
    public void Une_game_de_la_veille_heure_de_l_instance_n_est_pas_plausible()
    {
        // Game lancée à 23 h 30 à Paris le 17, clip publié à 0 h 30 le 18.
        var game = GamePlausible.Unique(Utc(10, 17, 22, 30), Paris, [(7, Utc(10, 17, 21, 30))]);

        Assert.Null(game);
    }

    [Fact]
    public void Sans_game_le_jour_de_publication_il_n_y_a_pas_de_game_plausible()
    {
        Assert.Null(GamePlausible.Unique(Utc(10, 17, 21), Paris, []));
    }
}
