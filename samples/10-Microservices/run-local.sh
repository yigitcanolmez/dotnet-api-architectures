#!/usr/bin/env bash
# RabbitMQ'yu docker-compose.infra.yml ile başlatır, 4 servisi `dotnet run` ile arka planda çalıştırır.
# Loglar: .local/logs/<servis>.log   PID'ler: .local/pids/<servis>.pid   Durdurmak için: ./stop-local.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"
mkdir -p .local/logs .local/pids

echo ">> RabbitMQ başlatılıyor..."
docker compose -f docker-compose.infra.yml up -d --wait

echo ">> Solution derleniyor..."
# --disable-build-servers: arka planda MSBuild/Roslyn sunucusu bırakma
dotnet build Microservices.slnx -nologo -v q --disable-build-servers

start() {
  local name="$1" dir="$2" port="$3"
  # Her servis kendi proje klasöründe çalışır → kendi .db dosyası orada oluşur (database-per-service).
  # exec: alt kabuk `dotnet run`'a dönüşür → PID doğrudan ona ait; çıktılar log dosyasına, terminale değil.
  (cd "$dir" && exec nohup dotnet run --no-build) >"$ROOT/.local/logs/$name.log" 2>&1 </dev/null &
  echo $! >"$ROOT/.local/pids/$name.pid"
  echo "   $name → http://localhost:$port (PID $(cat ".local/pids/$name.pid"))"
}

echo ">> Servisler başlatılıyor..."
start ordering  src/Services/Ordering/Ordering.Api   5101
start inventory src/Services/Inventory/Inventory.Api 5102
start payment   src/Services/Payment/Payment.Api     5103
start gateway   src/Gateway/Gateway.Api              5100

echo ">> Sağlık kontrolleri bekleniyor..."
for port in 5101 5102 5103 5100; do
  for _ in $(seq 1 60); do
    if curl -fs "http://localhost:$port/health" >/dev/null 2>&1; then echo "   :$port Healthy"; break; fi
    sleep 1
  done
done
echo ">> Hazır. Gateway: http://localhost:5100  RabbitMQ UI: http://localhost:15672 (guest/guest)"
