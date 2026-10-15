using Microsoft.Extensions.Time.Testing;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>
/// Horloge de l'API pendant les tests : un <see cref="FakeTimeProvider"/> qu'on peut aussi remettre en arrière,
/// puisque l'instance enregistrée dans l'API est la même pour toute la suite.
/// Ses minuteries n'avancent qu'avec <see cref="Advance"/>.
/// Le cookie de session suit aussi cette horloge : connecter les clients avant de la régler dans le passé,
/// sinon le cookie est déjà expiré pour le client HTTP.
/// </summary>
public sealed class FakeClock : TimeProvider
{
    private FakeTimeProvider _inner = new(DateTimeOffset.UtcNow);

    public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();

    public override TimeZoneInfo LocalTimeZone => _inner.LocalTimeZone;

    public override long TimestampFrequency => _inner.TimestampFrequency;

    public override long GetTimestamp() => _inner.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _inner.CreateTimer(callback, state, dueTime, period);

    /// <summary>Règle l'heure. Revenir en arrière abandonne les minuteries en cours.</summary>
    public void SetUtcNow(DateTimeOffset value)
    {
        if (value < _inner.GetUtcNow())
            _inner = new FakeTimeProvider(value);
        else
            _inner.SetUtcNow(value);
    }

    public void Advance(TimeSpan delta) => _inner.Advance(delta);

    /// <summary>Remet l'horloge à l'heure réelle, sans minuterie en cours.</summary>
    public void Reset() => _inner = new FakeTimeProvider(DateTimeOffset.UtcNow);
}
