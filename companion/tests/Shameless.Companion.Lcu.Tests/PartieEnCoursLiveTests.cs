using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Shameless.Companion.Lcu.Tests;

public class PartieEnCoursLiveTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static int PortLibre()
    {
        var ecouteur = new TcpListener(IPAddress.Loopback, 0);
        ecouteur.Start();
        var port = ((IPEndPoint)ecouteur.LocalEndpoint).Port;
        ecouteur.Stop();
        return port;
    }

    [Fact]
    public async Task Sans_partie_le_port_est_ferme_et_la_lecture_donne_null()
    {
        using var live = new PartieEnCoursLive($"https://127.0.0.1:{PortLibre()}");

        Assert.Null(await live.LireAsync(Ct));
    }

    [Fact]
    public async Task La_lecture_rend_le_corps_de_allgamedata_et_null_sur_un_statut_d_erreur()
    {
        var port = PortLibre();
        using var ecouteur = new HttpListener();
        ecouteur.Prefixes.Add($"http://127.0.0.1:{port}/");
        ecouteur.Start();
        var serveur = Task.Run(async () =>
        {
            var essai = 0;
            while (essai++ < 2)
            {
                var contexte = await ecouteur.GetContextAsync();
                Assert.Equal("/liveclientdata/allgamedata", contexte.Request.Url!.AbsolutePath);
                var corps = Encoding.UTF8.GetBytes("""{"gameData":{"gameTime":12.5}}""");
                contexte.Response.StatusCode = essai == 1 ? 200 : 404;
                await contexte.Response.OutputStream.WriteAsync(corps, Ct);
                contexte.Response.Close();
            }
        }, Ct);
        using var live = new PartieEnCoursLive($"http://127.0.0.1:{port}");

        Assert.Equal("""{"gameData":{"gameTime":12.5}}""", await live.LireAsync(Ct));
        Assert.Null(await live.LireAsync(Ct));
        await serveur;
    }
}
