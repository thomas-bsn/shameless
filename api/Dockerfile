FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /app
COPY *.sln .
COPY Custom5v5.Api/Custom5v5.Api.csproj Custom5v5.Api/
COPY Custom5v5.Application/Custom5v5.Application.csproj Custom5v5.Application/
COPY Custom5v5.Infrastructure/Custom5v5.Infrastructure.csproj Custom5v5.Infrastructure/
RUN dotnet restore
COPY . .
RUN dotnet publish Custom5v5.Api/Custom5v5.Api.csproj -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=builder /publish .
EXPOSE 8080
CMD ["dotnet", "Custom5v5.Api.dll"]
