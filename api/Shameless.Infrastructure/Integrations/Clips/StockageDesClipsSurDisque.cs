using Microsoft.Extensions.Options;
using Shameless.Application.Clips;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Integrations.Clips;

/// <summary>
/// Les vidéos des clips dans un dossier de l'Instance (<see cref="ClipsOptions.StoragePath"/>, un volume
/// dans le compose). Les noms sont des GUID : rien de ce que le bot envoie n'entre dans un chemin.
/// </summary>
public sealed class StockageDesClipsSurDisque : IStockageDesClips
{
    private readonly string _dossier;

    public StockageDesClipsSurDisque(IOptions<ClipsOptions> options)
    {
        _dossier = Path.GetFullPath(options.Value.StoragePath);
    }

    public async Task<string> EnregistrerAsync(Stream video, string extension, CancellationToken ct)
    {
        Directory.CreateDirectory(_dossier);
        var fichier = Guid.NewGuid().ToString("N") + extension;
        var chemin = CheminDe(fichier);
        var temporaire = chemin + ".part";
        try
        {
            await using (var sortie = new FileStream(temporaire, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await video.CopyToAsync(sortie, ct);
            File.Move(temporaire, chemin);
        }
        catch
        {
            File.Delete(temporaire);
            throw;
        }
        return fichier;
    }

    public string? Trouver(string fichier) => CheminDe(fichier) is var chemin && File.Exists(chemin) ? chemin : null;

    private string CheminDe(string fichier) => Path.Combine(_dossier, Path.GetFileName(fichier));

    public void Supprimer(string fichier) => File.Delete(CheminDe(fichier));
}
