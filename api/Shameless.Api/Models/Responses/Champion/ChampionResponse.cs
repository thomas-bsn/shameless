namespace Shameless.Api.Models.Responses.Champion;

/// <summary>Un champion du catalogue Data Dragon.</summary>
public sealed record ChampionResponse(
    string Id, // identifiant Data Dragon, ex. "LeeSin"
    string Key, // clé numérique Riot, ex. "64"
    string Name,
    string ImageUrl
);
