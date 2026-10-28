# Multi-stage build for the E piljara API (Render, Coolify, any Docker host).
# Build context je backend/:  docker build -t epiljara-api backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Prvo samo projektni fajlovi: restore sloj se kešira dok se paketi ne promijene.
COPY global.json Directory.Build.props ./
COPY src/EPiljara.Domain/EPiljara.Domain.csproj src/EPiljara.Domain/
COPY src/EPiljara.Application/EPiljara.Application.csproj src/EPiljara.Application/
COPY src/EPiljara.Infrastructure/EPiljara.Infrastructure.csproj src/EPiljara.Infrastructure/
COPY src/EPiljara.API/EPiljara.API.csproj src/EPiljara.API/
RUN dotnet restore src/EPiljara.API/EPiljara.API.csproj

COPY src/ ./src/
RUN dotnet publish src/EPiljara.API/EPiljara.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Rezervni port. Kad je PORT postavljen (Render), Program.cs sluša na njemu.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Ne-root korisnik koji aspnet image već ima.
USER $APP_UID
ENTRYPOINT ["dotnet", "EPiljara.API.dll"]
