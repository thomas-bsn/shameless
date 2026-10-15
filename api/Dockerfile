FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Shameless.sln ./
COPY Shameless.Api/Shameless.Api.csproj Shameless.Api/
COPY Shameless.Application/Shameless.Application.csproj Shameless.Application/
COPY Shameless.Infrastructure/Shameless.Infrastructure.csproj Shameless.Infrastructure/
RUN dotnet restore Shameless.Api/Shameless.Api.csproj
COPY . .
RUN dotnet publish Shameless.Api/Shameless.Api.csproj -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=builder /publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Shameless.Api.dll"]
