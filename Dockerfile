FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Fixturely.Domain/Fixturely.Domain.csproj src/Fixturely.Domain/
COPY src/Fixturely.Application/Fixturely.Application.csproj src/Fixturely.Application/
COPY src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj src/Fixturely.Infrastructure/
COPY src/Fixturely.Api/Fixturely.Api.csproj src/Fixturely.Api/

RUN dotnet restore src/Fixturely.Api/Fixturely.Api.csproj

COPY src/ src/

RUN dotnet publish src/Fixturely.Api/Fixturely.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN useradd --uid 5678 --user-group --shell /usr/sbin/nologin fixturely \
    && chown -R fixturely:fixturely /app

COPY --from=build /app/publish .

USER fixturely

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Fixturely.Api.dll"]
