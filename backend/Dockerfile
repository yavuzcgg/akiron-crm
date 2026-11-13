# API image. Build context: the repository root (the analyzer rules live in /.editorconfig).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached while only source files change.
COPY .editorconfig ./
COPY backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/ src/
RUN dotnet restore src/Akiron.Api/Akiron.Api.csproj
RUN dotnet publish src/Akiron.Api/Akiron.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Akiron.Api.dll"]
