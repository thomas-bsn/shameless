FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props dotnet-tools.json Shameless.sln ./
COPY Shameless.Api/Shameless.Api.csproj Shameless.Api/
COPY Shameless.Application/Shameless.Application.csproj Shameless.Application/
COPY Shameless.Infrastructure/Shameless.Infrastructure.csproj Shameless.Infrastructure/
RUN dotnet restore Shameless.Api/Shameless.Api.csproj && dotnet tool restore
COPY Shameless.Api/ Shameless.Api/
COPY Shameless.Application/ Shameless.Application/
COPY Shameless.Infrastructure/ Shameless.Infrastructure/
RUN dotnet publish Shameless.Api/Shameless.Api.csproj -c Release --no-restore -o /publish
RUN dotnet ef migrations bundle -p Shameless.Infrastructure -s Shameless.Infrastructure --configuration Release -o /bundle/efbundle

# Job de migration : applique la Baseline (et les suivantes) puis se termine.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrate
WORKDIR /app
COPY --from=build /bundle/efbundle .
# Le job écrit le marqueur de version dans le volume nommé /state, qui peut être root-owned
# (créé vide, ou par une ancienne image). L'image reste root pour pouvoir y écrire sans
# l'option Compose `user:` (refusée par Forgeyard) ; l'API, non-root, ne fait que le lire.
ENTRYPOINT ["sh", "-c", "./efbundle --connection \"$ConnectionStrings__Shameless\""]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /publish .
# Les volumes nommés montés ici (clés, vidéos des clips) héritent de ce propriétaire à leur création.
RUN mkdir /keys /clips && chown $APP_UID /keys /clips
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Shameless.Api.dll"]
