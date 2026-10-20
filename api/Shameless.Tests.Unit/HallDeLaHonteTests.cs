using Shameless.Application.Entities;
using Shameless.Application.Votes;

namespace Shameless.Tests.Unit;

/// <summary>Le Hall de la Honte : le cumul des titres de DOFD, les ex æquo au même rang.</summary>
public class HallDeLaHonteTests
{
    private static Player Joueur(int id, string prenom) => new() { Id = id, Prenom = prenom, RiotId = $"{prenom}#EUW" };

    [Fact]
    public void Les_joueurs_sont_classes_par_dofd_decroissant_et_les_ex_aequo_partagent_le_rang()
    {
        var joueurs = new[] { Joueur(1, "Zed"), Joueur(2, "Ana"), Joueur(3, "Bob"), Joueur(4, "Cid") };
        var titres = new Dictionary<int, TitresDuJoueur>
        {
            [1] = new(3),
            [2] = new(3),
            [3] = new(1),
            [4] = new(0),
        };

        var hall = HallDeLaHonte.Classer(joueurs, titres);

        Assert.Equal(
            [(1, 2, "Ana", 3), (1, 1, "Zed", 3), (3, 3, "Bob", 1)],
            hall.Joueurs.Select(l => (l.Rang, l.PlayerId, l.Prenom, l.Dofd)));
    }

    [Fact]
    public void Sans_aucun_dofd_le_hall_est_vide()
    {
        var hall = HallDeLaHonte.Classer([Joueur(1, "Ana")], new Dictionary<int, TitresDuJoueur>());

        Assert.Empty(hall.Joueurs);
    }
}
