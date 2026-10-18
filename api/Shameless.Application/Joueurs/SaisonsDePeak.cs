namespace Shameless.Application.Joueurs;

/// <summary>
/// Les saisons qu'on peut choisir pour un Peak : de S24 (2024) à la saison courante, qui suit l'année civile
/// (S26 en 2026). Seule la plage est contrôlée, jamais le rang lui-même.
/// </summary>
public static class SaisonsDePeak
{
    public const int AnneePremiereSaison = 2024;

    public static string Courante(int annee) => Libelle(annee);

    public static IReadOnlyList<string> Jusqua(int annee) =>
        Enumerable.Range(AnneePremiereSaison, Math.Max(0, annee - AnneePremiereSaison + 1)).Select(Libelle).ToList();

    public static bool EstValide(string? saison, int anneeCourante) =>
        saison is not null && Jusqua(anneeCourante).Contains(saison);

    private static string Libelle(int annee) => $"S{annee % 100:00}";
}
