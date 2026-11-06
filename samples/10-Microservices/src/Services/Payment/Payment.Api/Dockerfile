# syntax=docker/dockerfile:1
# Multi-stage build. Build context: samples/10-Microservices
# Ek build context "samplesroot" (= samples/) ortak Directory.Build.props için gerekir:
#   docker build --build-context samplesroot=.. -f src/Services/Payment/Payment.Api/Dockerfile -t microservices-payment .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY --from=samplesroot Directory.Build.props ./
WORKDIR /src/10-Microservices
# Önce sadece csproj'lar → restore katmanı cache'lenir
COPY src/Shared/Contracts/Contracts.csproj src/Shared/Contracts/
COPY src/Services/Payment/Payment.Api/Payment.Api.csproj src/Services/Payment/Payment.Api/
RUN dotnet restore src/Services/Payment/Payment.Api/Payment.Api.csproj
COPY src/Shared/Contracts/ src/Shared/Contracts/
COPY src/Services/Payment/Payment.Api/ src/Services/Payment/Payment.Api/
RUN dotnet publish src/Services/Payment/Payment.Api/Payment.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# SQLite dosyası için yazılabilir klasör; container root olmayan kullanıcıyla çalışır.
RUN mkdir -p /app/data && chown $APP_UID /app/data
COPY --from=build /app/publish .
USER $APP_UID
ENV Urls=http://+:5103
EXPOSE 5103
ENTRYPOINT ["dotnet", "Payment.Api.dll"]
