#!/usr/bin/env bash
# Legacy (5201), Modern (5202) ve Proxy (5200) uygulamalarını birlikte başlatır. Ctrl+C ile hepsi durur.
set -euo pipefail
cd "$(dirname "$0")"

dotnet build StranglerFig.slnx -v q -nologo

PIDS=()
start() {
  local name=$1
  # Proje klasöründen çalıştırıyoruz: content root = proje klasörü → appsettings.json değişiklikleri (flag) canlı okunur.
  ( cd "src/$name" && exec dotnet "bin/Debug/net10.0/$name.dll" ) &
  PIDS+=($!)
  echo "▶ $name başlatıldı (PID $!)"
}

cleanup() {
  echo; echo "Durduruluyor: ${PIDS[*]}"
  for pid in "${PIDS[@]}"; do kill "$pid" 2>/dev/null || true; done
}
trap cleanup EXIT INT TERM

start Legacy.Api
start Modern.Api
start Proxy

echo
echo "Proxy:  http://localhost:5200   (istemcilerin bildiği TEK adres)"
echo "Legacy: http://localhost:5201   Modern: http://localhost:5202"
echo "Deneyin: curl -i http://localhost:5200/api/orders | grep X-Served-By"
wait
