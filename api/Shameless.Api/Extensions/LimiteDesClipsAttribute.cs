using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Shameless.Application.Clips;

namespace Shameless.Api.Extensions;

/// <summary>
/// Relève, pour cette route seulement, les limites de taille du corps (Kestrel : 30 Mo) et du multipart
/// (128 Mo) à la taille maximale d'un clip (<see cref="ClipsOptions.MaxFileSizeBytes"/>), plus de quoi
/// porter les champs du formulaire. Filtre de ressource : il passe après la clé du bot, avant la lecture
/// du corps.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LimiteDesClipsAttribute : Attribute, IResourceFilter
{
    private const long Marge = 1024 * 1024;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var http = context.HttpContext;
        var limite = http.RequestServices.GetRequiredService<IOptions<ClipsOptions>>().Value.MaxFileSizeBytes + Marge;

        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } corps)
            corps.MaxRequestBodySize = limite;
        if (http.Features.Get<IFormFeature>()?.Form is null)
            http.Features.Set<IFormFeature>(new FormFeature(http.Request, new FormOptions { MultipartBodyLengthLimit = limite }));
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
