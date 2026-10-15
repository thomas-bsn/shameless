using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

public enum CloseSoireeResult
{
    Closed,
    NotFound,
    AlreadyClosed
}

public sealed class SoireeService
{
    private readonly ISoireeRepository _soirees;

    public SoireeService(ISoireeRepository soirees) => _soirees = soirees;

    public Task<Soiree?> GetOpenAsync() => _soirees.GetOpenAsync();

    /// <summary>Ouvre une Soirée. Renvoie null si une Soirée est déjà ouverte.</summary>
    public async Task<Soiree?> OpenAsync()
    {
        if (await _soirees.GetOpenAsync() != null) return null;

        var soiree = new Soiree { Status = SoireeStatus.Open, OpenedAt = DateTime.UtcNow };
        return await _soirees.TryAddAsync(soiree) ? soiree : null;
    }

    public async Task<(CloseSoireeResult Result, Soiree? Soiree)> CloseAsync(int id)
    {
        var soiree = await _soirees.GetByIdAsync(id);
        if (soiree == null) return (CloseSoireeResult.NotFound, null);
        if (soiree.Status == SoireeStatus.Closed) return (CloseSoireeResult.AlreadyClosed, soiree);

        soiree.Status = SoireeStatus.Closed;
        soiree.ClosedAt = DateTime.UtcNow;
        await _soirees.SaveChangesAsync();
        return (CloseSoireeResult.Closed, soiree);
    }
}
