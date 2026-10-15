using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Application.Services;

/// <summary>
/// Les commandes du Picker de la Soirée ouverte : cocher les dix joueurs, tirer (ou relancer) la
/// Roulette, valider le tirage pour créer la Série. Les algorithmes sont dans <see cref="Roulette"/>.
/// </summary>
public sealed class PickerService
{
    public const int PlayersCount = 2 * Roulette.PlayersPerTeam;

    private readonly ISoireeRepository _soirees;
    private readonly IPickerSessionRepository _sessions;
    private readonly IPlayerRepository _players;
    private readonly SeriesService _series;
    private readonly IRandomSource _random;

    public PickerService(
        ISoireeRepository soirees,
        IPickerSessionRepository sessions,
        IPlayerRepository players,
        SeriesService series,
        IRandomSource random)
    {
        _soirees = soirees;
        _sessions = sessions;
        _players = players;
        _series = series;
        _random = random;
    }

    /// <summary>La session de Picker de la Soirée, ou null.</summary>
    public Task<PickerSession?> GetAsync(int soireeId) => _sessions.GetBySoireeAsync(soireeId);

    /// <summary>
    /// Coche les dix joueurs : ouvre la session de Picker de la Soirée ouverte, ou remplace sa
    /// sélection (le tirage en cours est alors effacé).
    /// </summary>
    public async Task<CommandResult<PickerSession>> SelectAsync(IReadOnlyCollection<int> playerIds)
    {
        if (playerIds.Count != PlayersCount || playerIds.Distinct().Count() != PlayersCount)
            return CommandResult<PickerSession>.Invalid("Le Picker exige exactement dix joueurs distincts.");
        if ((await _players.GetByIdsAsync(playerIds.ToList())).Count != PlayersCount)
            return CommandResult<PickerSession>.Invalid("Un des joueurs n'existe pas.");

        var soiree = await _soirees.GetOpenAsync();
        if (soiree == null)
            return NoOpenSoiree();

        var session = await _sessions.GetBySoireeAsync(soiree.Id);
        if (session == null)
        {
            session = new PickerSession { SoireeId = soiree.Id };
            session.Select(playerIds);
            if (!await _sessions.TryAddAsync(session))
                return CommandResult<PickerSession>.Conflict("Une session de Picker vient d'être ouverte dans cette Soirée.");
        }
        else
        {
            session.Select(playerIds);
            await _sessions.SaveChangesAsync();
        }

        return CommandResult<PickerSession>.Ok((await _sessions.GetBySoireeAsync(soiree.Id))!);
    }

    /// <summary>Tire les Équipes par Roulette ; tirer à nouveau relance et remplace le tirage courant.</summary>
    public async Task<CommandResult<PickerSession>> TirerRouletteAsync(RouletteVariante variante)
    {
        var (session, refus) = await GetSessionOfOpenSoireeAsync();
        if (session == null) return refus!;

        var joueurs = session.Players.Select(p => p.PlayerId).ToList();
        var composition = variante == RouletteVariante.Equilibree
            ? Roulette.Equilibree(session.Players.Select(p => new PickerPlayer(p.PlayerId, p.Player.CurrentCost)).ToList(), _random)
            : Roulette.Aleatoire(joueurs, _random);

        session.ApplyRoulette(variante, composition);
        await _sessions.SaveChangesAsync();
        return CommandResult<PickerSession>.Ok(session);
    }

    /// <summary>Valide la composition courante : crée la Série entre les deux Équipes et clôt la session.</summary>
    public async Task<CommandResult<BestOfSeries>> ValidateAsync(int format, bool fearless, bool alternateSides)
    {
        var (session, refus) = await GetSessionOfOpenSoireeAsync();
        if (session == null) return refus!.As<BestOfSeries>();
        if (!session.CanValidate)
            return CommandResult<BestOfSeries>.Conflict("Les deux Équipes ne sont pas encore formées : tirez-les avant de valider.");

        var created = await _series.CreateAsync(
            session.PlayerIdsOf(Equipes.A), session.PlayerIdsOf(Equipes.B), format, fearless, alternateSides);
        if (!created.IsOk) return created;

        await _sessions.RemoveAsync(session);
        return created;
    }

    private async Task<(PickerSession? Session, CommandResult<PickerSession>? Refus)> GetSessionOfOpenSoireeAsync()
    {
        var soiree = await _soirees.GetOpenAsync();
        if (soiree == null) return (null, NoOpenSoiree());

        var session = await _sessions.GetBySoireeAsync(soiree.Id);
        return session == null
            ? (null, CommandResult<PickerSession>.Conflict("Aucune session de Picker : cochez d'abord les dix joueurs."))
            : (session, null);
    }

    private static CommandResult<PickerSession> NoOpenSoiree() =>
        CommandResult<PickerSession>.Conflict("Aucune Soirée n'est ouverte : ouvrez-en une pour lancer le Picker.");
}
