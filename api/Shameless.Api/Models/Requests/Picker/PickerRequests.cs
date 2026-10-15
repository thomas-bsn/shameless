using Shameless.Application.Entities;
using Shameless.Application.Picker;

namespace Shameless.Api.Models.Requests.Picker;

/// <summary>Les dix joueurs cochés pour la prochaine Série.</summary>
public sealed record SelectPlayersRequest(List<int> PlayerIds);

/// <summary>Un tirage de Roulette : "Aleatoire" ou "Equilibree". Tirer à nouveau relance.</summary>
public sealed record TirerRouletteRequest(RouletteVariante Variante);

/// <summary>
/// Lance une Draft. Capitaines "Manuels" (avec <c>CapitaineA</c> et <c>CapitaineB</c>), "AuHasard" ou
/// "PlusGrosCosts". Premier pick "A" ou "B", ou absent pour pile ou face.
/// </summary>
public sealed record LancerDraftRequest(
    DesignationCapitaines Capitaines, int? CapitaineA = null, int? CapitaineB = null, string? PremierPick = null);

/// <summary>Un pick de la Draft : le capitaine de l'Équipe ("A" ou "B") prend le joueur.</summary>
public sealed record PickRequest(string Equipe, int PlayerId);

/// <summary>Un Autorole : "HorsRole" ou "Aleatoire". Lancer à nouveau relance.</summary>
public sealed record AutoroleRequest(AutoroleVariante Variante);

/// <summary>Valide le tirage : la Série au format 1, 2, 3 ou 5, avec ses options.</summary>
public sealed record ValidatePickerRequest(int Format, bool Fearless = false, bool AlternateSides = false);
