using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shameless.Api;
using Shameless.Api.Cli;
using Shameless.Application.Entities;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>La commande « superadmin ajouter | retirer | lister » de l'image de l'API (ADR 0017).</summary>
public class SuperadminCommandTests(ApiFactory factory) : IntegrationTest(factory)
{
    private IConfiguration Config => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Shameless"] = Factory.ConnectionString,
            ["Instance:TimeZone"] = "Europe/Paris"
        })
        .Build();

    private async Task<(int Code, string Out, string Err)> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await SuperadminCli.RunAsync(["superadmin", .. args], Config, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task Lister_montre_le_superadministrateur_de_la_configuration_avec_sa_promotion()
    {
        var (code, output, _) = await RunAsync("lister");

        Assert.Equal(0, code);
        var jour = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"));
        Assert.Contains($"{TestSession.AdminDiscordId}", output);
        Assert.Contains("promu par configuration, le ", output);
        Assert.Matches(@"le \d{2}/\d{2}/\d{4}", output);
        Assert.Contains(jour.ToString("yyyy"), output);
    }

    [Fact]
    public async Task Ajouter_cree_un_superadministrateur_promu_par_la_ligne_de_commande_et_c_est_idempotent()
    {
        var premier = await RunAsync("ajouter", "5150");
        var second = await RunAsync("ajouter", "5150");

        Assert.Equal(0, premier.Code);
        Assert.Equal(0, second.Code);
        var (_, liste, _) = await RunAsync("lister");
        Assert.Single(liste.Split('\n', StringSplitOptions.RemoveEmptyEntries), l => l.StartsWith("5150 "));
        Assert.Contains("promu par ligne de commande, le ", liste);
    }

    [Fact]
    public async Task Retirer_refuse_le_dernier_superadministrateur_puis_accepte_quand_il_y_en_a_un_autre()
    {
        var refus = await RunAsync("retirer", TestSession.AdminDiscordId);
        Assert.Equal(1, refus.Code);
        Assert.Contains("dernier Superadministrateur", refus.Err);

        await RunAsync("ajouter", "5151");
        var ok = await RunAsync("retirer", TestSession.AdminDiscordId);

        Assert.Equal(0, ok.Code);
        await using var db = Factory.CreateDbContext();
        Assert.Equal(UserRole.Utilisateur, (await db.Users.SingleAsync(u => u.DiscordId == 9000, Ct)).Role);
        Assert.Equal(UserRole.Superadministrateur, (await db.Users.SingleAsync(u => u.DiscordId == 5151, Ct)).Role);
    }

    [Fact]
    public async Task Retirer_refuse_quelqu_un_qui_n_est_pas_superadministrateur()
    {
        var refus = await RunAsync("retirer", "123456");

        Assert.Equal(1, refus.Code);
        Assert.Contains("n'est pas Superadministrateur", refus.Err);
    }

    [Theory]
    [InlineData]
    [InlineData("ajouter")]
    [InlineData("ajouter", "abc")]
    [InlineData("ajouter", "-3")]
    [InlineData("retirer", "1", "2")]
    [InlineData("lister", "1")]
    [InlineData("supprimer", "1")]
    public async Task Un_usage_incorrect_affiche_l_aide_sans_toucher_a_la_base(params string[] args)
    {
        var (code, output, error) = await RunAsync(args);

        Assert.Equal(2, code);
        Assert.Empty(output);
        Assert.Contains("superadmin ajouter", error);
        await using var db = Factory.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync(Ct));
    }

    [Fact]
    public void Le_fichier_instance_env_du_wizard_se_relit_avec_ses_apostrophes()
    {
        var env = InstanceEnv.Parse("Auth__AdminDiscordIds='1,2'\nConnectionStrings__Shameless='Host=db;Password=a'\\''b c'\n\n# x\nVIDE=''\n");

        Assert.Equal("1,2", env["Auth__AdminDiscordIds"]);
        Assert.Equal("Host=db;Password=a'b c", env["ConnectionStrings__Shameless"]);
        Assert.Equal("", env["VIDE"]);
    }

    /// <summary>Le vrai point d'entrée : <c>dotnet Shameless.Api.dll superadmin …</c>, base lue dans un instance.env.</summary>
    [Fact]
    public async Task Le_point_d_entree_de_l_image_lance_la_commande_avec_la_configuration_de_instance_env()
    {
        var dossier = Directory.CreateTempSubdirectory("shameless-cli-");
        try
        {
            var instanceEnv = Path.Combine(dossier.FullName, "instance.env");
            await File.WriteAllTextAsync(instanceEnv,
                $"ConnectionStrings__Shameless='{Factory.ConnectionString.Replace("'", "'\\''")}'\n", Ct);

            var (code, output, error) = await LancerAsync(instanceEnv, "ajouter", "5152");
            Assert.True(code == 0, error);
            Assert.Contains("5152", output);

            var (codeLister, lister, _) = await LancerAsync(instanceEnv, "lister");
            Assert.Equal(0, codeLister);
            Assert.Contains("5152", lister);

            var (codeUsage, _, usage) = await LancerAsync(instanceEnv, "oups");
            Assert.Equal(2, codeUsage);
            Assert.Contains("Usage", usage);
        }
        finally
        {
            dossier.Delete(recursive: true);
        }
    }

    private async Task<(int Code, string Out, string Err)> LancerAsync(string instanceEnv, params string[] args)
    {
        var dll = typeof(Program).Assembly.Location;
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(dll)!
        };
        info.ArgumentList.Add(dll);
        info.ArgumentList.Add("superadmin");
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment["SHAMELESS_INSTANCE_ENV"] = instanceEnv;
        info.Environment.Remove("ConnectionStrings__Shameless");

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(Ct);
        var error = process.StandardError.ReadToEndAsync(Ct);
        await process.WaitForExitAsync(Ct);
        return (process.ExitCode, await output, await error);
    }
}
