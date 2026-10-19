using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public class DraftChampionsRepository : IDraftChampionsRepository
{
    private readonly AppDbContext _db;

    public DraftChampionsRepository(AppDbContext db) => _db = db;

    public void Add(DraftChampions draft) => _db.DraftsChampions.Add(draft);

    public void Remove(DraftChampions draft) => _db.DraftsChampions.Remove(draft);

    public Task<DraftChampions?> GetByGameAsync(int gameId) =>
        Complete().SingleOrDefaultAsync(d => d.GameId == gameId);

    public Task<DraftChampions?> GetDerniereOfSeriesAsync(int seriesId) =>
        Complete().Where(d => d.Game.SeriesId == seriesId).OrderByDescending(d => d.GameId).FirstOrDefaultAsync();

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();

    public Task<List<MessageDraftChampions>> GetMessagesAsync(int draftId) =>
        _db.MessagesDraftChampions.AsNoTracking().Where(m => m.DraftChampionsId == draftId).OrderBy(m => m.Id).ToListAsync();

    public Task<int> CountMessagesDepuisAsync(int draftId, string emetteur, DateTime depuis) =>
        _db.MessagesDraftChampions.CountAsync(m => m.DraftChampionsId == draftId && m.Emetteur == emetteur && m.EnvoyeLe > depuis);

    public Task<string?> GetAuteurAsync(int draftId, string emetteur) =>
        _db.MessagesDraftChampions.Where(m => m.DraftChampionsId == draftId && m.Emetteur == emetteur)
            .Select(m => (string?)m.Auteur).FirstOrDefaultAsync();

    public async Task<int> DernierNumeroSpectateurAsync(int draftId) =>
        await _db.MessagesDraftChampions.Where(m => m.DraftChampionsId == draftId)
            .MaxAsync(m => m.NumeroSpectateur) ?? 0;

    public Task<bool> TryAddMessageAsync(MessageDraftChampions message) => _db.TryAddAsync(message);

    public async Task<bool> TryAddMessageSpectateurAsync(MessageDraftChampions message)
    {
        // Les nouveaux spectateurs d'une même draft passent un par un (verrou consultatif tenu jusqu'à la fin
        // de la transaction) : chacun lit le dernier numéro après l'enregistrement du précédent.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({VerrouNumerotationSpectateurs}, {message.DraftChampionsId})");

        var connu = await GetAuteurAsync(message.DraftChampionsId, message.Emetteur);
        if (connu != null)
            message.Auteur = connu;
        else
        {
            message.NumeroSpectateur = await DernierNumeroSpectateurAsync(message.DraftChampionsId) + 1;
            message.Auteur = $"Spectateur {message.NumeroSpectateur}";
        }

        if (!await _db.TryAddAsync(message)) return false;
        await transaction.CommitAsync();
        return true;
    }

    private const int VerrouNumerotationSpectateurs = 7152;

    private IQueryable<DraftChampions> Complete() =>
        _db.DraftsChampions
            .Include(d => d.Actions.OrderBy(a => a.Numero))
            .Include(d => d.Game).ThenInclude(g => g.Participants).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .Include(d => d.Game).ThenInclude(g => g.Series).ThenInclude(s => s!.Players)
            .Include(d => d.Game).ThenInclude(g => g.Series).ThenInclude(s => s!.Games).ThenInclude(g => g.Participants)
            .AsSplitQuery();
}
