using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public sealed class EvenementBotRepository : IEvenementBotRepository
{
    private readonly AppDbContext _db;

    public EvenementBotRepository(AppDbContext db) => _db = db;

    public void Add(EvenementBot evenement) => _db.EvenementsBot.Add(evenement);

    public Task<List<EvenementBot>> GetALivrerAsync(DateTime maintenant) =>
        _db.EvenementsBot
            .Where(e => e.Statut == StatutEvenementBot.ALivrer && e.ProchaineTentative <= maintenant)
            .OrderBy(e => e.Id)
            .Include(e => e.Series).ThenInclude(s => s!.Players).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .Include(e => e.Series).ThenInclude(s => s!.Liens)
            .Include(e => e.Game).ThenInclude(g => g!.Participants).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .Include(e => e.Game).ThenInclude(g => g!.Series).ThenInclude(s => s!.Games)
            .Include(e => e.Game).ThenInclude(g => g!.Series).ThenInclude(s => s!.Players)
            .AsSplitQuery()
            .ToListAsync();

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();
}
