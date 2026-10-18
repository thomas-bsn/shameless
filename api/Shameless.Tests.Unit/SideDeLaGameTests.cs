using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Tests.Unit;

public class SideDeLaGameTests
{
    private sealed class Tirage(params int[] valeurs) : IRandomSource
    {
        private readonly Queue<int> _valeurs = new(valeurs);
        public int Next(int maxExclusive) => _valeurs.Dequeue();
    }

    [Theory]
    [InlineData(0, Sides.Blue)]
    [InlineData(1, Sides.Red)]
    public void Le_side_de_la_premiere_game_est_tire_a_pile_ou_face(int tirage, string attendu)
    {
        Assert.Equal(attendu, Sides.ProchainDeLEquipeA(null, new Tirage(tirage)));
    }

    [Theory]
    [InlineData(Sides.Blue, Sides.Red)]
    [InlineData(Sides.Red, Sides.Blue)]
    public void Les_games_suivantes_alternent_sans_tirage(string precedent, string attendu)
    {
        // Un tirage demandé ferait échouer le test : la file est vide.
        Assert.Equal(attendu, Sides.ProchainDeLEquipeA(precedent, new Tirage()));
    }
}
