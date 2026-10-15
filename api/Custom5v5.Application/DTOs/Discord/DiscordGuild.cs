using System.Text.Json.Serialization;

namespace Custom5v5.Application.DTOs.Discord;

public sealed record DiscordGuild(
    [property: JsonPropertyName("id")] string Id
);