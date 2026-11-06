# syntax=docker/dockerfile:1
# Multi-stage build. Build context: samples/10-Microservices
# Ek build context "samplesroot" (= samples/) ortak Directory.Build.props için gerekir:
#   docker build --build-context samplesroot=.. -f src/Services/Inventory/Inventory.Api/Dockerfile -t microservices-inventory .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=samplesroot Directory.Build.props ./
WORKDIR /src/10-Microservices
# Önce sadece csproj'lar → restore katmanı cache'lenir
COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/Services/Inventory/Inventory.Api/Inventory.Api.csproj src/Services/Inventory/Inventory.Api/
RUN dotnet restore src/Services/Inventory/Inventory.Api/Inventory.Api.csproj
COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Inventory/Inventory.Api/ src/Services/Inventory/Inventory.Api/
RUN dotnet publish src/Services/Inventory/Inventory.Api/Inventory.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# SQLite dosyası için yazılabilir klasör; container root olmayan kullanıcıyla çalışır.
RUN mkdir -p /app/data && chown $APP_UID /app/data
COPY --from=build /app/publish .
USER $APP_UID
ENV Urls=http://+:5102
EXPOSE 5102
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]
