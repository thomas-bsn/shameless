FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /app
COPY *.sln .
COPY Shameless.Api/Shameless.Api.csproj Shameless.Api/
COPY Shameless.Application/Shameless.Application.csproj Shameless.Application/
COPY Shameless.Infrastructure/Shameless.Infrastructure.csproj Shameless.Infrastructure/
RUN dotnet restore
COPY . .
RUN dotnet publish Shameless.Api/Shameless.Api.csproj -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=builder /publish .
EXPOSE 8080
CMD ["dotnet", "Shameless.Api.dll"]
