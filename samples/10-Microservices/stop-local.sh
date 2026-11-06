#!/usr/bin/env bash
# run-local.sh ile başlatılan servisleri PID ile durdurur ve RabbitMQ container'ını kaldırır.
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

# `dotnet run` asıl uygulamayı alt süreç olarak başlatır → süreç ağacını yapraktan köke PID ile durdur.
kill_tree() {
  local p="$1"
  for child in $(ps -o pid= --ppid "$p" 2>/dev/null); do kill_tree "$child"; done
  kill "$p" 2>/dev/null || true
}

for f in .local/pids/*.pid; do
  [ -e "$f" ] || continue
  pid="$(cat "$f")"
  kill_tree "$pid"
  echo "Durduruldu: $(basename "$f" .pid) (PID $pid)"
  rm -f "$f"
done

docker compose -f docker-compose.infra.yml down
