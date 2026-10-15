using System.Text.Json;
using Shameless.Api.Interfaces;
using Shameless.Application.DTOs.Players;
using Microsoft.AspNetCore.Hosting;

namespace Shameless.Api.Services;

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