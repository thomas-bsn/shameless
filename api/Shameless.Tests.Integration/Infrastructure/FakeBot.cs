using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>
/// Le bot Discord simulé à la frontière HTTP : le vrai client de l'API l'appelle (clé, signature,
/// enveloppe), il répond ce que le test a prévu et garde chaque requête reçue.
/// </summary>
public sealed class FakeBot : HttpMessageHandler
{
    public const string Url = "https://bot.test";
    public const string ApiKey = "cle-de-l-api-vers-le-bot";
    public const string WebhookSecret = "secret-des-webhooks";

    /// <summary>Une requête reçue par le bot : méthode, chemin, en-têtes utiles et corps brut.</summary>
    public sealed record Requete(string Method, string Path, string? Authorization, string? Timestamp, string? Signature, byte[] Body)
    {
        public string Texte => Encoding.UTF8.GetString(Body);
    }

    private readonly ConcurrentQueue<Requete> _requetes = new();
    private readonly ConcurrentQueue<Func<HttpResponseMessage>> _reponsesWebhook = new();

    /// <summary>La réponse de <c>GET /internal/shameless/voice-candidates</c>.</summary>
    public Func<HttpResponseMessage> CandidatsVocaux { get; set; } = () => Json("""{"channel_id":null,"member_ids":[]}""");

    public IReadOnlyList<Requete> Requetes => _requetes.ToList();

    public IReadOnlyList<Requete> Webhooks => Requetes.Where(r => r.Path == "/internal/shameless/webhook").ToList();

    /// <summary>Les prochaines réponses du webhook, dans l'ordre ; ensuite 200 <c>processed</c>.</summary>
    public void RepondreAuWebhook(params Func<HttpResponseMessage>[] reponses)
    {
        foreach (var reponse in reponses) _reponsesWebhook.Enqueue(reponse);
    }

    public void Reset()
    {
        _requetes.Clear();
        _reponsesWebhook.Clear();
        CandidatsVocaux = () => Json("""{"channel_id":null,"member_ids":[]}""");
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static Func<HttpResponseMessage> Statut(HttpStatusCode status, string body = "{}") => () => Json(body, status);

    public static Func<HttpResponseMessage> Panne() => () => throw new HttpRequestException("bot injoignable");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        _requetes.Enqueue(new Requete(
            request.Method.Method,
            request.RequestUri!.AbsolutePath,
            request.Headers.Authorization?.ToString(),
            Header(request, "X-Shameless-Timestamp"),
            Header(request, "X-Shameless-Signature"),
            body));

        return request.RequestUri.AbsolutePath switch
        {
            "/internal/shameless/voice-candidates" => CandidatsVocaux(),
            "/internal/shameless/webhook" => _reponsesWebhook.TryDequeue(out var reponse)
                ? reponse()
                : Json("""{"status":"processed"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
}
