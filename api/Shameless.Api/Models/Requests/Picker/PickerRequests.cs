using Shameless.Application.Entities;

namespace Shameless.Api.Models.Requests.Picker;

/// <summary>Les dix joueurs cochés pour la prochaine Série.</summary>
public sealed record SelectPlayersRequest(List<int> PlayerIds);

/// <summary>Un tirage de Roulette : "Aleatoire" ou "Equilibree". Tirer à nouveau relance.</summary>
public sealed record TirerRouletteRequest(RouletteVariante Variante);

/// <summary>Valide le tirage : la Série au format 1, 2, 3 ou 5, avec ses options.</summary>
public sealed record ValidatePickerRequest(int Format, bool Fearless = false, bool AlternateSides = false);
