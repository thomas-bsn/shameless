using Shameless.Application;

namespace Shameless.Tests.Unit;

/// <summary>L'heure lue par les services : tronquée à la microseconde, la précision de PostgreSQL.</summary>
public class HorlogeTests
{
    private sealed class HorlogeFixe(DateTimeOffset maintenant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => maintenant;
    }

    [Fact]
    public void Maintenant_est_tronque_a_la_microseconde()
    {
        var brut = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero).AddTicks(1234567);

        var lu = new HorlogeFixe(brut).MaintenantUtc();

        Assert.Equal(brut.UtcDateTime.AddTicks(-7), lu);
        Assert.Equal(0, lu.Ticks % 10);
        Assert.Equal(DateTimeKind.Utc, lu.Kind);
    }
}
