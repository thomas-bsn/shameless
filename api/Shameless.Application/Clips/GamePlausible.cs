namespace Shameless.Application.Clips;

/// <summary>
/// La Game plausible d'un Clip (ADR 0014) : une Game lancée le jour calendaire de publication du clip,
/// dans le fuseau de l'Instance, au plus tard à l'instant de la publication. Le clip n'est rattaché
/// automatiquement que s'il y en a exactement une.
/// </summary>
public static class GamePlausible
{
    /// <summary>Le jour calendaire, dans le fuseau de l'Instance, qui contient l'instant donné : bornes UTC [Debut, Fin[.</summary>
    public static (DateTime Debut, DateTime Fin) JourDePublication(DateTime publieLeUtc, TimeZoneInfo fuseau)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(publieLeUtc, DateTimeKind.Utc), fuseau);
        var minuit = DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified);
        return (EnUtc(minuit, fuseau), EnUtc(minuit.AddDays(1), fuseau));
    }

    /// <summary>
    /// L'id de l'unique Game plausible parmi les games données (id, lancée le, UTC), ou null s'il n'y en a
    /// aucune ou plusieurs.
    /// </summary>
    public static int? Unique(DateTime publieLeUtc, TimeZoneInfo fuseau, IEnumerable<(int Id, DateTime LanceeLe)> games)
    {
        var (debut, _) = JourDePublication(publieLeUtc, fuseau);
        var plausibles = games.Where(g => g.LanceeLe >= debut && g.LanceeLe <= publieLeUtc).Select(g => g.Id).Distinct().ToList();
        return plausibles.Count == 1 ? plausibles[0] : null;
    }

    /// <summary>
    /// Les bornes UTC [Debut, Fin] des games à proposer pour rattacher un clip à la main : lancées la veille
    /// ou le jour de publication (fuseau de l'Instance), au plus tard à l'instant de la publication. La
    /// veille couvre un clip publié après minuit pour une game de la soirée.
    /// </summary>
    public static (DateTime Debut, DateTime Fin) FenetreDesCandidates(DateTime publieLeUtc, TimeZoneInfo fuseau)
    {
        var (jour, _) = JourDePublication(publieLeUtc, fuseau);
        var (veille, _) = JourDePublication(jour.AddTicks(-1), fuseau);
        return (veille, publieLeUtc);
    }

    // Minuit n'est jamais dans un trou de changement d'heure en Europe ; ailleurs, on prend l'heure valide suivante.
    private static DateTime EnUtc(DateTime local, TimeZoneInfo fuseau)
    {
        while (fuseau.IsInvalidTime(local)) local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(local, fuseau);
    }
}
