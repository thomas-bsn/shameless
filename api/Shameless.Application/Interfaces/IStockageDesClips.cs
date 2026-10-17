namespace Shameless.Application.Interfaces;

/// <summary>Les vidéos des clips, dans le stockage de l'Instance (un dossier sur un volume, ADR 0014).</summary>
public interface IStockageDesClips
{
    /// <summary>Écrit la vidéo sous un nouveau nom, choisi par le stockage, et renvoie ce nom.</summary>
    Task<string> EnregistrerAsync(Stream video, string extension, CancellationToken ct);

    /// <summary>Le chemin absolu du fichier, pour le servir.</summary>
    string CheminDe(string fichier);

    /// <summary>Supprime le fichier s'il existe.</summary>
    void Supprimer(string fichier);
}
