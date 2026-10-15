using System.Text.Json;
using Custom5v5.Api.Interfaces;
using Custom5v5.Application.DTOs.Players;
using Microsoft.AspNetCore.Hosting;

namespace Custom5v5.Api.Services;

public sealed class JsonPlayersSource : IPlayersSource
{
    private readonly IReadOnlyList<PlayerDto> _players;

    public JsonPlayersSource(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "players.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"players.json not found at {path}");

        var json = File.ReadAllText(path);

        var players = JsonSerializer.Deserialize<List<PlayerDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<PlayerDto>();

        if (players.Count == 0)
            throw new InvalidOperationException("players.json is empty.");

        // Defensive copy + read-only
        _players = players.AsReadOnly();
    }

    // Snapshot synchrone utilisé par PollStore à l'ouverture du poll
    public IReadOnlyList<PlayerDto> GetAllSnapshot()
        => _players;
}