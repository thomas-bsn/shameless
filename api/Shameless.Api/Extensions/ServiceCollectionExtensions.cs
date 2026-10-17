using System.Text.Json.Serialization;
using Shameless.Api.Interfaces;
using Shameless.Api.Services;
using Shameless.Application.BackgroundJobs;
using Shameless.Application.Bot;
using Shameless.Application.Interfaces;
using Shameless.Application.Services;
using Shameless.Infrastructure.Data.Repositories;
using Shameless.Infrastructure.Repositories;

namespace Shameless.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Background jobs
        services.AddHostedService<RefreshRanksJob>();
        services.AddHostedService<FermetureAutomatiqueSoireeJob>();
        services.AddHostedService<PublicationWebhooksBotJob>();

        // Repositories
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomGameRepository, CustomGameRepository>();
        services.AddScoped<ICustomGameParticipantRepository, CustomGameParticipantRepository>();
        services.AddScoped<IPlayerMonthlyCostRepository, PlayerMonthlyCostRepository>();
        services.AddScoped<IBestOfSeriesRepository, BestOfSeriesRepository>();
        services.AddScoped<ISoireeRepository, SoireeRepository>();
        services.AddScoped<IPickerSessionRepository, PickerSessionRepository>();
        services.AddScoped<IDraftChampionsRepository, DraftChampionsRepository>();
        services.AddScoped<IEvenementBotRepository, EvenementBotRepository>();
        services.AddScoped<IVotesRepository, VotesRepository>();

        // Services
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<CustomGameService>();
        services.AddScoped<ClassementsService>();
        services.AddScoped<FicheJoueurService>();
        services.AddScoped<CostCalculationService>();
        services.AddScoped<SoireeService>();
        services.AddScoped<SeriesService>();
        services.AddScoped<PickerService>();
        services.AddScoped<LiensAccesService>();
        services.AddScoped<CodeCompanionService>();
        services.AddScoped<RapportsCompanionService>();
        services.AddScoped<DraftChampionsService>();
        services.AddScoped<CandidatsVocauxService>();
        services.AddScoped<EvenementsBot>();
        services.AddScoped<VotesService>();

        // Singletons
        services.AddSingleton<IOAuthStateStore, OAuthStateStore>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRandomSource, SharedRandomSource>();

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers(o =>
            {
                o.Filters.Add<CatalogueIndisponibleFilter>();
                o.Filters.Add<BotIndisponibleFilter>();
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                // Les nombres restent des nombres : sinon le contrat OpenAPI les type « integer | string ».
                o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            });

        // Lues par le générateur OpenAPI pour décrire les schémas : mêmes réglages que les contrôleurs,
        // sinon les enums (écrites en texte) seraient décrites comme des entiers.
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });
        services.AddOpenApi(options => options.AddShamelessContract());
        services.AddProblemDetails();

        return services;
    }
}