namespace Shameless.Application;

public static class HorlogeExtensions
{
    /// <summary>
    /// L'heure UTC tronquée à la microseconde, la précision de PostgreSQL : ce qu'on renvoie juste après une
    /// action est ainsi identique à ce qu'on relit en base ensuite. À utiliser pour tout horodatage persisté.
    /// </summary>
    public static DateTime MaintenantUtc(this TimeProvider horloge)
    {
        var maintenant = horloge.GetUtcNow().UtcDateTime;
        return maintenant.AddTicks(-(maintenant.Ticks % 10));
    }
}
