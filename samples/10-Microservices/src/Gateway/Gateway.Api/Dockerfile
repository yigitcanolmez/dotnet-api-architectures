# syntax=docker/dockerfile:1
# Multi-stage build. Build context: samples/10-Microservices
# Ek build context "samplesroot" (= samples/) ortak Directory.Build.props için gerekir:
#   docker build --build-context samplesroot=.. -f src/Gateway/Gateway.Api/Dockerfile -t microservices-gateway .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=samplesroot Directory.Build.props ./
WORKDIR /src/10-Microservices
# Gateway, Contracts'a bağlı değil → sadece kendi projesi kopyalanır.
COPY src/Gateway/Gateway.Api/Gateway.Api.csproj src/Gateway/Gateway.Api/
RUN dotnet restore src/Gateway/Gateway.Api/Gateway.Api.csproj
COPY src/Gateway/Gateway.Api/ src/Gateway/Gateway.Api/
RUN dotnet publish src/Gateway/Gateway.Api/Gateway.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV Urls=http://+:5100
EXPOSE 5100
ENTRYPOINT ["dotnet", "Gateway.Api.dll"]
