using System.Text.Json.Serialization;

namespace Shameless.Application.DTOs.Discord;

public sealed record DiscordGuild(
    [property: JsonPropertyName("id")] string Id
);