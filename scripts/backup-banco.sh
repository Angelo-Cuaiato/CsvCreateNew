#!/usr/bin/env bash
# Cópia de segurança do banco, para rodar na máquina onde o compose está de pé.
#
#   ./scripts/backup-banco.sh                 # grava em ./backups
#   ./scripts/backup-banco.sh /mnt/backups    # grava onde você mandar
#
# Restauração:
#   gunzip -c backups/fluxocaixa-2026-09-01.sql.gz | \
#     docker compose exec -T db psql -U fluxo -d fluxocaixa
set -euo pipefail

DESTINO="${1:-./backups}"
mkdir -p "$DESTINO"

# Lê as credenciais do mesmo .env que o compose usa.
[ -f .env ] && set -a && . ./.env && set +a
USUARIO="${POSTGRES_USER:-fluxo}"
BANCO="${POSTGRES_DB:-fluxocaixa}"

ARQUIVO="$DESTINO/$BANCO-$(date +%Y-%m-%d-%H%M).sql.gz"

docker compose exec -T db pg_dump --username "$USUARIO" --dbname "$BANCO" --clean --if-exists \
  | gzip > "$ARQUIVO"

echo "Backup gravado em $ARQUIVO ($(du -h "$ARQUIVO" | cut -f1))"

# Mantém os 14 mais recentes.
ls -1t "$DESTINO"/"$BANCO"-*.sql.gz 2>/dev/null | tail -n +15 | xargs -r rm --
