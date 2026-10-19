namespace Shameless.Companion.Lcu.Tests;

public class LockfileTests
{
    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"lol-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Lit_port_et_mot_de_passe_depuis_le_fichier()
    {
        var path = Path.Combine(NewDir(), "lockfile");
        File.WriteAllText(path, "LeagueClient:4242:51234:s3cr3t-pass:https");

        var lockfile = Lockfile.Read(path);

        Assert.Equal(51234, lockfile.Port);
        Assert.Equal("s3cr3t-pass", lockfile.Password);
        Assert.Equal("https", lockfile.Protocol);
    }

    [Fact]
    public void Dit_clairement_quand_le_fichier_est_absent()
    {
        var path = Path.Combine(NewDir(), "lockfile");

        var ex = Assert.Throws<FileNotFoundException>(() => Lockfile.Read(path));

        Assert.Contains(path, ex.Message);
    }

    [Fact]
    public void Refuse_un_contenu_mal_forme()
    {
        var path = Path.Combine(NewDir(), "lockfile");
        File.WriteAllText(path, "n'importe quoi");

        Assert.Throws<FormatException>(() => Lockfile.Read(path));
    }

    [Fact]
    public void Trouve_le_lockfile_dans_le_dossier_du_client_en_cours_d_execution()
    {
        var installation = NewDir();
        var lockfile = Path.Combine(installation, "lockfile");
        File.WriteAllText(lockfile, "LeagueClient:1:2:pw:https");

        Assert.Equal(lockfile, Lockfile.Locate(null, [installation], "/introuvable/lockfile"));
    }

    [Fact]
    public void Le_chemin_explicite_passe_avant_le_client_detecte()
    {
        var installation = NewDir();
        File.WriteAllText(Path.Combine(installation, "lockfile"), "LeagueClient:1:2:pw:https");

        Assert.Equal("/choisi/lockfile", Lockfile.Locate("/choisi/lockfile", [installation], "/introuvable/lockfile"));
    }

    [Fact]
    public void Ignore_un_dossier_de_client_sans_lockfile_et_prend_le_suivant()
    {
        var vide = NewDir();
        var installation = NewDir();
        var lockfile = Path.Combine(installation, "lockfile");
        File.WriteAllText(lockfile, "LeagueClient:1:2:pw:https");

        Assert.Equal(lockfile, Lockfile.Locate(null, [vide, installation], "/introuvable/lockfile"));
    }

    [Fact]
    public void Retombe_sur_le_chemin_par_defaut_quand_rien_n_est_detecte()
    {
        Assert.Equal("/defaut/lockfile", Lockfile.Locate(null, [], "/defaut/lockfile"));
    }

    [Fact]
    public void Le_chemin_par_defaut_est_celui_de_Riot_Games_sous_Windows()
    {
        Assert.Equal(@"C:\Riot Games\League of Legends\lockfile", Lockfile.DefaultPath);
    }
}
