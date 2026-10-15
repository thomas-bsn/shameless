using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Shameless.Api.Extensions;

public static class OpenApiExtensions
{
    private const string ProblemSchema = "ProblemDetails";

    /// <summary>
    /// Contrat consommé par le client TypeScript du front : sans serveur (le front l'appelle
    /// sous /api), chaque propriété sérialisée est requise, et toute erreur est décrite
    /// comme un ProblemDetails.
    /// </summary>
    public static OpenApiOptions AddShamelessContract(this OpenApiOptions options)
    {
        // System.Text.Json écrit toutes les propriétés, null compris : elles sont toujours présentes.
        // ProblemDetails fait exception, ses champs vides sont omis.
        options.AddSchemaTransformer((schema, context, _) =>
        {
            if (schema.Properties is { Count: > 0 } properties && context.JsonTypeInfo.Type != typeof(ProblemDetails))
                schema.Required = new HashSet<string>(properties.Keys);
            return Task.CompletedTask;
        });

        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Servers = [];
            return Task.CompletedTask;
        });

        options.AddOperationTransformer(async (operation, context, ct) =>
        {
            if (context.Document is not { } document)
                return;

            if (document.Components?.Schemas?.ContainsKey(ProblemSchema) != true)
                document.AddComponent(ProblemSchema, await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, ct));

            operation.Responses ??= new OpenApiResponses();
            operation.Responses["default"] = new OpenApiResponse
            {
                Description = "Erreur",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference(ProblemSchema, document) }
                }
            };
        });

        return options;
    }
}
