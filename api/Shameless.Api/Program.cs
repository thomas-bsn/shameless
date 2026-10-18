using Shameless.Api.Cli;
using Shameless.Api.Extensions;
using Shameless.Api.Startup;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Commande de l'image (ADR 0017) : « dotnet Shameless.Api.dll superadmin ajouter | retirer | lister ».
if (SuperadminCli.Concerne(args))
    return await SuperadminCli.RunAsync(args, InstanceEnv.Load(), Console.Out, Console.Error);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSessionAuthentication(builder.Configuration);
builder.Services.AddHttpClients();
builder.Services.AddApplicationServices();
builder.Services.AddApiServices();

var app = builder.Build();

app.ConfigureApplication();

app.Run();
return 0;
public partial class Program;
