namespace Shameless.Application.Entities;

/// <summary>
/// Un Spectateur identifié par le bot comme présent dans un salon vocal pendant la Game : il peut
/// voter pour le MVP et le DOFD de cette Game.
/// </summary>
public class SpectateurDeGame
{
    public int GameId { get; set; }
    public CustomGame Game { get; set; } = null!;

    /// <summary>L'ID Discord (snowflake) du Spectateur.</summary>
    public long DiscordId { get; set; }
}
