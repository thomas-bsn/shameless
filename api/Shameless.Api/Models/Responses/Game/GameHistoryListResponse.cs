using Shameless.Application.DTOs.Match;

namespace Shameless.Api.Models.Responses.Game;

/// <summary>Les séries d'un mois. Le nom « games » est gardé de l'ancien front.</summary>
public sealed record GameHistoryListResponse(List<SeriesHistoryResponse> Games);
