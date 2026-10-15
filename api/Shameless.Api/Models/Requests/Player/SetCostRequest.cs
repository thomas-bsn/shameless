using System.ComponentModel.DataAnnotations;

namespace Shameless.Api.Models.Requests.Player;

public sealed class SetCostRequest
{
    [Range(1, 6)]
    public decimal Cost { get; init; }
}
