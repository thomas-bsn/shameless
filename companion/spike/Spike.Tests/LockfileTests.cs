using Spike;

namespace Spike.Tests;

public class LockfileTests
{
    [Fact]
    public void Lit_port_et_mot_de_passe_depuis_le_fichier()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lockfile-{Guid.NewGuid():N}");
        File.WriteAllText(path, "LeagueClient:4242:51234:s3cr3t-pass:https");

        var lockfile = Lockfile.Read(path);

        Assert.Equal(51234, lockfile.Port);
        Assert.Equal("s3cr3t-pass", lockfile.Password);
        Assert.Equal("https", lockfile.Protocol);
    }

    [Fact]
    public void Dit_clairement_quand_le_fichier_est_absent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}");

        var ex = Assert.Throws<FileNotFoundException>(() => Lockfile.Read(path));

        Assert.Contains(path, ex.Message);
        Assert.Contains("client LoL", ex.Message);
    }

    [Fact]
    public void Refuse_un_contenu_mal_forme()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lockfile-{Guid.NewGuid():N}");
        File.WriteAllText(path, "n'importe quoi");

        Assert.Throws<FormatException>(() => Lockfile.Read(path));
    }
}
