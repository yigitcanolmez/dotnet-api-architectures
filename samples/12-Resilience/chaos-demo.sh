#!/usr/bin/env bash
# Dayanıklılık senaryolarını otomatik koşturur ve özet basar.
# Servisler çalışmıyorsa kendisi derleyip başlatır ve sonunda (PID ile) durdurur.
set -uo pipefail
cd "$(dirname "$0")"

CLIENT=http://localhost:5300
SVC=http://localhost:5301
KLAVYE=11111111-1111-1111-1111-111111111111
MOUSE=22222222-2222-2222-2222-222222222222
MONITOR=33333333-3333-3333-3333-333333333333
N=${N:-20}
PIDS=()

up() { curl -s -o /dev/null "$1/" ; }

start() {
  local name=$1
  ( cd "src/$name" && exec dotnet "bin/Debug/net10.0/$name.dll" ) > "$name.demo.log" 2>&1 &
  PIDS+=($!)
}

cleanup() {
  curl -s -o /dev/null -X POST "$SVC/chaos" -H 'Content-Type: application/json' -d '{"mode":"healthy"}' 2>/dev/null
  if [ ${#PIDS[@]} -gt 0 ]; then
    for pid in "${PIDS[@]}"; do kill "$pid" 2>/dev/null; done
    rm -f ./*.demo.log
  fi
}
trap cleanup EXIT

if ! up $SVC || ! up $CLIENT; then
  echo "Servisler başlatılıyor..."
  dotnet build Resilience.slnx -v q -nologo > /dev/null || { echo "build başarısız"; exit 1; }
  up $SVC || start UnstableService.Api
  up $CLIENT || start ResilienceClient.Api
  for _ in $(seq 1 30); do up $SVC && up $CLIENT && break; sleep 0.5; done
fi

chaos()  { curl -s -X POST "$SVC/chaos" -H 'Content-Type: application/json' -d "$1" > /dev/null; }
reset()  { curl -s -o /dev/null -X POST "$SVC/stats/reset"; }
total()  { curl -s "$SVC/stats" | sed -E 's/.*"totalRequests":([0-9]+).*/\1/'; }
code()   { curl -s -o /dev/null -w "%{http_code}" "$1"; }

# $1=endpoint yolu → "başarılı/N  | downstream istek sayısı"
measure() {
  reset
  local ok=0
  for _ in $(seq 1 "$N"); do [ "$(code "$CLIENT$1")" = "200" ] && ok=$((ok+1)); done
  printf "  %-50s başarı: %2d/%d   downstream'e giden istek: %s\n" "$1" "$ok" "$N" "$(total)"
}

header() { echo; echo "━━━ $1 ━━━"; }

header "1) Sağlıklı servis (baseline)"
chaos '{"mode":"healthy"}'
measure "/naive/$KLAVYE"

header "2) flaky %50 → naive vs retry (retry: 3 tekrar, exponential + jitter)"
chaos '{"mode":"flaky","failureRate":0.5}'
measure "/naive/$KLAVYE"
measure "/retry/$KLAVYE"
echo "  → Retry başarı oranını ~%50'den ~%94'e çıkarır (1-0.5⁴) AMA downstream yükü artar (retry storm riski)."

header "3) flaky %20 → AddStandardResilienceHandler (retry + circuit breaker + timeout'lar)"
chaos '{"mode":"flaky","failureRate":0.2}'
measure "/standard/$KLAVYE"

header "4) slow 3000 ms → naive vs timeout (attempt timeout 1 sn)"
chaos '{"mode":"slow","latencyMs":3000}'
printf "  /naive   → HTTP %s\n" "$(curl -s -o /dev/null -w '%{http_code} (%{time_total} sn)' "$CLIENT/naive/$KLAVYE")"
printf "  /timeout → HTTP %s\n" "$(curl -s -o /dev/null -w '%{http_code} (%{time_total} sn)' "$CLIENT/timeout/$KLAVYE")"
echo "  → naive 3 sn bekler (HttpClient.Timeout varsayılanı 100 sn!), timeout 1 sn'de 504 ile vazgeçer."

header "5) slow (isteklerin %50'si 2000 ms) → naive vs hedging (300 ms sonra paralel deneme)"
chaos '{"mode":"slow","latencyMs":2000,"latencyRate":0.5}'
for ep in naive hedging; do
  reset
  avg=$(for _ in $(seq 1 10); do curl -s -o /dev/null -w '%{time_total}\n' "$CLIENT/$ep/$KLAVYE"; done | awk '{s+=$1} END {printf "%.2f", s/NR}')
  printf "  /%-8s ortalama süre: %s sn   downstream'e giden istek: %s (10 çağrı için)\n" "$ep" "$avg" "$(total)"
done
echo "  → Hedging kuyruk gecikmesini düşürür; bedeli ekstra downstream isteğidir."

header "6) Bulkhead: slow 2000 ms + 8 paralel istek (limit: 2 eşzamanlı + 1 kuyruk)"
chaos '{"mode":"slow","latencyMs":2000}'
reset
results=$(for _ in $(seq 1 8); do curl -s -o /dev/null -w "%{http_code}\n" "$CLIENT/bulkhead/$KLAVYE" & done; wait)
printf "  200: %s   503 (reddedildi): %s   downstream maxConcurrent: %s\n" \
  "$(grep -c 200 <<< "$results")" "$(grep -c 503 <<< "$results")" \
  "$(curl -s "$SVC/stats" | sed -E 's/.*"maxConcurrent":([0-9]+).*/\1/')"

header "7) Circuit breaker: servis down → 12 çağrı"
chaos '{"mode":"down"}'
reset
line=""
for _ in $(seq 1 12); do line+="$(code "$CLIENT/circuit-breaker/$MOUSE") "; done
echo "  HTTP kodları: $line"
echo "  Devre durumu: $(curl -s "$CLIENT/circuit-breaker/state")   downstream'e giden istek: $(total)/12"
echo "  → 502'ler gerçek hatalar; devre açılınca 503'ler ağa ÇIKMADAN döner (downstream korunur)."

header "8) Fallback: önce canlı fiyat cache'lenir, sonra servis down"
chaos '{"mode":"healthy"}'
echo "  canlı      : $(curl -s "$CLIENT/fallback/$MONITOR")"
chaos '{"mode":"down"}'
echo "  son bilinen: $(curl -s "$CLIENT/fallback/$MONITOR")"
echo "  varsayılan : $(curl -s "$CLIENT/fallback/$KLAVYE")"
chaos '{"mode":"healthy"}'

header "9) Gelen istek rate limiting (token bucket: 5 jeton / 10 sn) → 8 hızlı istek"
line=""
for _ in $(seq 1 8); do line+="$(code "$CLIENT/rate-limited") "; done
echo "  HTTP kodları: $line"

echo
echo "Bitti. Chaos 'healthy' moduna döndürüldü."
