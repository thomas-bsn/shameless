using Custom5v5.Api.Extensions;
using Custom5v5.Api.Startup;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication();
builder.Services.AddHttpClients();
builder.Services.AddApplicationServices();
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

await app.ConfigureApplicationAsync();

app.Run();