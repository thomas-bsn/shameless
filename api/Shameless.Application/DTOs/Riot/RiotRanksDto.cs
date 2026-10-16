namespace Shameless.Application.DTOs.Riot;

/// <summary>Les rangs actuels d'un compte Riot par file classée ; null quand le compte n'est pas classé dans la file.</summary>
/// <param name="Solo">File RANKED_SOLO_5x5 (le Rang SoloQ).</param>
/// <param name="Flex">File RANKED_FLEX_SR.</param>
public sealed record RiotRanksDto(RiotRankDto? Solo, RiotRankDto? Flex);
