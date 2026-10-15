namespace Custom5v5.Application.Services;

public static class MMRCalculationService
{
    // Mapping Tier → MMR de base (aligné avec ton frontend)
    private static readonly Dictionary<string, int> TierBaseMMR = new()
    {
        { "UNRANKED", 0 },
        { "IRON", 800 },
        { "BRONZE", 1000 },
        { "SILVER", 1200 },
        { "GOLD", 1450 },
        { "PLATINUM", 1700 },
        { "EMERALD", 2000 },
        { "DIAMOND", 2300 },
        { "MASTER", 2600 },
        { "GRANDMASTER", 2900 },
        { "CHALLENGER", 3200 }
    };
    
    // Mapping Division → offset MMR (aligné avec ton frontend)
    private static readonly Dictionary<int, int> DivisionOffset = new()
    {
        { 4, 0 },  // IV
        { 3, 25 }, // III
        { 2, 50 }, // II
        { 1, 75 }  // I
    };
    
    /// <summary>
    /// Calcule le MMR de base à partir du peak rank SoloQ
    /// </summary>
    public static int GetPeakRankMMR(string? tier, int? division)
    {
        if (string.IsNullOrEmpty(tier)) return 0; // Unranked
        
        var baseMMR = TierBaseMMR.GetValueOrDefault(tier.ToUpper(), 0);
        
        // Master+ n'ont pas de divisions
        if (tier.ToUpper() is "MASTER" or "GRANDMASTER" or "CHALLENGER")
        {
            return baseMMR;
        }
        
        var divOffset = DivisionOffset.GetValueOrDefault(division ?? 4, 0);
        
        return baseMMR + divOffset;
    }
    
    /// <summary>
    /// Calcule le bonus/malus MMR basé sur le winrate du mois
    /// Formule : (winrate - 50%) * 150 * confidence
    /// </summary>
    public static int CalculateWinrateAdjustment(int gamesPlayed, int wins)
    {
        // Minimum 3 games pour avoir un ajustement
        if (gamesPlayed < 3) return 0;
        
        var winrate = wins / (float)gamesPlayed;
        
        // Confidence scale avec le nombre de games (max à 20 games)
        var confidence = Math.Min(gamesPlayed / 20f, 1f);
        
        // Max ±150 MMR d'ajustement
        var maxAdjustment = 150;
        
        var adjustment = (winrate - 0.5f) * maxAdjustment * confidence;
        
        return (int)adjustment;
    }
    
    /// <summary>
    /// Calcule le MMR final (SoloQ + Winrate)
    /// </summary>
    public static int CalculateFinalMMR(string? tier, int? division, int gamesPlayed, int wins)
    {
        var baseMMR = GetPeakRankMMR(tier, division);
        var winrateBonus = CalculateWinrateAdjustment(gamesPlayed, wins);
        
        return baseMMR + winrateBonus;
    }
    
    /// <summary>
    /// Convertit un MMR en Cost (1-6)
    /// </summary>
    public static decimal MMRToCost(int mmr)
    {
        return mmr switch
        {
            >= 2800 => 6m,  // SS Tier - GM/Challenger
            >= 2400 => 5m,  // S  Tier - Master
            >= 2000 => 4m,  // A  Tier - Diamond/Emerald high
            >= 1600 => 3m,  // B  Tier - Platinum/Emerald low
            >= 1200 => 2m,  // C  Tier - Gold/Silver
            _       => 1m   // D  Tier - Bronze/Iron/Unranked
        };
    }
    
    /// <summary>
    /// Convertit un Cost en label tier (pour affichage)
    /// </summary>
    public static string GetTierLabel(decimal cost)
    {
        return cost switch
        {
            6m => "SS",
            5m => "S",
            4m => "A",
            3m => "B",
            2m => "C",
            1m => "D",
            _ => "Unranked"
        };
    }
    
    /// <summary>
    /// Récupère la couleur CSS pour un tier (optionnel, pour UI)
    /// </summary>
    public static string GetTierColor(decimal cost)
    {
        return cost switch
        {
            6m => "#FF6B6B", // SS - Rouge
            5m => "#FFD93D", // S  - Or
            4m => "#6BCF7F", // A  - Vert
            3m => "#4ECDC4", // B  - Cyan
            2m => "#95A5A6", // C  - Gris
            1m => "#BDC3C7", // D  - Gris clair
            _ => "#34495E"   // Unranked - Gris foncé
        };
    }
}