# Multi-stage build for the E piljara API (Coolify / any Docker host).
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY EPiljara.sln ./
COPY src/ ./src/
RUN dotnet restore EPiljara.sln
RUN dotnet publish src/EPiljara.API -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "EPiljara.API.dll"]
