using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Shameless.Companion.Core;

namespace Shameless.Companion.App;

/// <summary>
/// Identifiants chiffrés avec DPAPI (portée utilisateur Windows) dans %LOCALAPPDATA%\Shameless\Companion.
/// Windows seulement : compile partout, mais n'a pas été exécuté hors Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class StockageDpapi(string chemin) : IStockageIdentifiants
{
    public static StockageDpapi Par_defaut() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Shameless", "Companion", "identifiants.bin"));

    public IdentifiantsCompanion? Lire()
    {
        try
        {
            if (!File.Exists(chemin)) return null;
            var clair = ProtectedData.Unprotect(File.ReadAllBytes(chemin), null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<IdentifiantsCompanion>(clair);
        }
        catch (Exception e) when (e is CryptographicException or JsonException or IOException)
        {
            // Fichier illisible (autre utilisateur, corrompu) : comme s'il n'y avait rien.
            return null;
        }
    }

    public void Ecrire(IdentifiantsCompanion identifiants)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);
        var clair = JsonSerializer.SerializeToUtf8Bytes(identifiants);
        File.WriteAllBytes(chemin, ProtectedData.Protect(clair, null, DataProtectionScope.CurrentUser));
    }

    public void Effacer()
    {
        if (File.Exists(chemin)) File.Delete(chemin);
    }
}
