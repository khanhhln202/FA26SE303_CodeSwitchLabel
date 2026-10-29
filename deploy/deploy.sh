#!/usr/bin/env bash
# =============================================================================
# C陂ｯ・ｭp nh陂ｯ・ｭt API ・・鮪ng ch陂ｯ・｡y tr・・ｽｪn m・・ｽ｡y ch逶ｻ・ｧ 遯ｶ繝ｻd・・ｽｹng thay cho quy tr・・ｽｬnh g・・ｽｵ tay trong
# deploy/README.md. Ch陂ｯ・｡y t逶ｻ・ｫ th・・ｽｰ m逶ｻ・･c g逶ｻ逾・c逶ｻ・ｧa repo tr・・ｽｪn m・・ｽ｡y ch逶ｻ・ｧ:
#
#     ./deploy/deploy.sh
#
# Th逶ｻ・ｩ t逶ｻ・ｱ c逶ｻ繝ｻ・・ｻ幢ｽｻ譚ｵh: ki逶ｻ繝・tra tr・・ｽｰ逶ｻ雖ｩ -> sao l・・ｽｰu -> k・・ｽｩo code -> migration -> d逶ｻ・ｱng
# l陂ｯ・｡i container -> ki逶ｻ繝・tra. Kh・・ｽｴng b・・ｽｰ逶ｻ雖ｩ n・・｣ｰo t逶ｻ・ｱ ・・ｽｽ ・・ｻ幢ｽｻ・･ng d逶ｻ・ｯ li逶ｻ緕｡ th陂ｯ・ｭt n陂ｯ・ｿu b・・ｽｰ逶ｻ雖ｩ
# tr・・ｽｰ逶ｻ雖ｩ ch・・ｽｰa th・・｣ｰnh c・・ｽｴng.
#
# GI陂ｯ・｢ ・・妛・ｻ蜉ｾH QUAN TR逶ｻ蜷姆: migration ph陂ｯ・｣i T・・ｽｯ・・｣ｰNG TH・・椶H NG・・ｽｯ逶ｻ・｢C v逶ｻ螫・code ・・鮪ng ch陂ｯ・｡y
# (ki逶ｻ繝・expand/contract 遯ｶ繝ｻth・・ｽｪm c逶ｻ蜀ｲ/b陂ｯ・｣ng m逶ｻ螫・ ch・・ｽｰa xo・・ｽ｡ c・・ｽ｡i c・・ｽｩ). V・・ｽｬ v陂ｯ・ｭy m逶ｻ螫・・・ｽ｡p
# migration TR・・ｽｯ逶ｻ蜥ｾ khi d逶ｻ・ｱng l陂ｯ・｡i container: code c・・ｽｩ v陂ｯ・ｫn ch陂ｯ・｡y ・・氈・ｰ逶ｻ・｣c v逶ｻ螫・l・・ｽｰ逶ｻ・｣c ・・ｻ幢ｽｻ繝ｻ# m逶ｻ螫・ Migration ph・・ｽ｡ v逶ｻ・｡ t・・ｽｰ・・ｽ｡ng th・・ｽｭch (xo・・ｽ｡ c逶ｻ蜀ｲ, ・・ｻ幢ｽｻ蜩・ki逶ｻ繝・ th・・ｽｬ ph陂ｯ・｣i l・・｣ｰm ng・・ｽｰ逶ｻ・｣c
# l陂ｯ・｡i 遯ｶ繝ｻd逶ｻ・ｱng container tr・・ｽｰ逶ｻ雖ｩ, migration sau 遯ｶ繝ｻv・・｣ｰ s逶ｻ・ｭa th逶ｻ・ｩ t逶ｻ・ｱ trong file n・・｣ｰy.
#
# Script KH・・ｹｴG BAO GI逶ｻ繝ｻt逶ｻ・ｱ ph逶ｻ・･c h逶ｻ螯ｬ database. Ph逶ｻ・･c h逶ｻ螯ｬ t逶ｻ・ｫ b陂ｯ・｣n sao l・・ｽｰu t陂ｯ・｡o
# v・・｣ｰi ph・・ｽｺt tr・・ｽｰ逶ｻ雖ｩ s陂ｯ・ｽ xo・・ｽ｡ m陂ｯ・･t d逶ｻ・ｯ li逶ｻ緕｡ th陂ｯ・ｭt ghi trong kho陂ｯ・｣ng ・・ｦ･・ｳ. Khi h逶ｻ辭殀, script
# in ra ・・ｦ･・ｺng c・・ｽ｢u l逶ｻ繻ｻh c陂ｯ・ｧn ch陂ｯ・｡y r逶ｻ螯ｬ d逶ｻ・ｫng, ・・ｻ幢ｽｻ繝ｻng・・ｽｰ逶ｻ諡ｱ tr逶ｻ・ｱc quy陂ｯ・ｿt ・・ｻ幢ｽｻ譚ｵh.
# =============================================================================

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

COMPOSE=(docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env)
MIGRATIONS_DIR="migrations"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/codeswitchlabel}"
KEEP_BACKUPS=7
# Cho phep chinh: may B2s dung yeu co the can lau hon khi vua build xong.
#   HEALTH_MAX_TRIES=120 HEALTH_INTERVAL=5 ./deploy/deploy.sh
HEALTH_MAX_TRIES="${HEALTH_MAX_TRIES:-60}"
HEALTH_INTERVAL="${HEALTH_INTERVAL:-2}"
PG_USER="csl"
PG_DB="codeswitchlabel"
PG_VOLUME="codeswitchlabel_pg-data"
MINIO_VOLUME="codeswitchlabel_minio-data"

say()  { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
warn() { printf '\033[1;33m[CANH BAO] %s\033[0m\n' "$*" >&2; }

# L逶ｻ繻ｻh ho・・｣ｰn t・・ｽ｡c. In ra khi deploy h逶ｻ辭殀 遯ｶ繝ｻnh・・ｽｰng KH・・ｹｴG t逶ｻ・ｱ ch陂ｯ・｡y: ph逶ｻ・･c h逶ｻ螯ｬ b陂ｯ・｣n
# sao l・・ｽｰu s陂ｯ・ｽ xo・・ｽ｡ m陂ｯ・･t d逶ｻ・ｯ li逶ｻ緕｡ th陂ｯ・ｭt ghi sau th逶ｻ諡ｱ ・・ｨｴ逶ｻ繝・sao l・・ｽｰu.
print_rollback() {
  cat >&2 <<ROLLBACK

--- Ban can CANH TAY LAM MOT TRONG CAC LENH SAU ---

1. Loi LUC DANG CHAY API (container moi khong len):
     docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env logs --tail 100 api

2. L・・ｽｹi code v逶ｻ繝ｻb陂ｯ・｣n tr・・ｽｰ逶ｻ雖ｩ, r逶ｻ螯ｬ d逶ｻ・ｱng l陂ｯ・｡i:
     git checkout ${OLD_SHA:-<sha cu>}
     docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
     git checkout main && git reset --hard origin/main

3. CH逶ｻ繝ｻkhi migration ・・ｦ･・｣ ch陂ｯ・｡y v・・｣ｰ d逶ｻ・ｯ li逶ｻ緕｡ h逶ｻ辭殀, m逶ｻ螫・ph逶ｻ・･c h逶ｻ螯ｬ database
   (L・δM M陂ｯ・､T D逶ｻ・ｮ LI逶ｻ繝ｻ GHI SAU TH逶ｻ蟒ｬ ・・摯逶ｻ繝ｻ SAO L・・ｽｯU):
     ls -lh $BACKUP_DIR
     gunzip -c <file>.sql.gz | docker exec -i csl-postgres psql -U $PG_USER -d $PG_DB -v ON_ERROR_STOP=1

4. Neu MinIO dang dung (sau backup that bai):
     docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env start minio

ROLLBACK
}

# `die` t逶ｻ・ｱ tho・・ｽ｡t n・・ｽｪn KH・・ｹｴG k・・ｽｭch ho陂ｯ・｡t trap ERR 遯ｶ繝ｻph陂ｯ・｣i in l逶ｻ繻ｻh ho・・｣ｰn t・・ｽ｡c 逶ｻ繝ｻ・・ｦ･・｢y,
# n陂ｯ・ｿu kh・・ｽｴng th・・ｽｬ h逶ｻ辭殀 逶ｻ繝ｻb・・ｽｰ逶ｻ雖ｩ ki逶ｻ繝・tra API s陂ｯ・ｽ kh・・ｽｴng c・・ｽｳ h・・ｽｰ逶ｻ螫ｾg d陂ｯ・ｫn n・・｣ｰo.
die()  { printf '\033[1;31m[DUNG] %s\033[0m\n' "$*" >&2; print_rollback; exit 1; }

# L逶ｻ謫・b陂ｯ・･t k逶ｻ・ｳ c逶ｻ・ｧa l逶ｻ繻ｻh -> in l逶ｻ繻ｻh ho・・｣ｰn t・・ｽ｡c r逶ｻ螯ｬ d逶ｻ・ｫng. Kh・・ｽｴng t逶ｻ・ｱ ph逶ｻ・･c h逶ｻ螯ｬ.
on_error() {
  local code=$?
  printf '\n\033[1;31m[LOI] Deploy dung giua chung (buoc %s).\033[0m\n' "${STEP:-?}" >&2
  print_rollback
  exit "$code"
}
trap on_error ERR

# -----------------------------------------------------------------------------
# 1. KI逶ｻ繝ｻ TRA TR・・ｽｯ逶ｻ蜥ｾ 遯ｶ繝ｻth陂ｯ・･t b陂ｯ・｡i 逶ｻ繝ｻ・・ｦ･・｢y th・・ｽｬ ch・・ｽｰa ・・ｻ幢ｽｻ・･ng g・・ｽｬ c陂ｯ・｣
# -----------------------------------------------------------------------------
STEP="kiem tra"

[ -f deploy/.env ] || die "Khong thay deploy/.env. Copy tu deploy/.env.example va dien gia tri that."

command -v curl >/dev/null || die "May chu thieu curl."
docker compose version >/dev/null 2>&1 || die "May chu thieu docker compose (v2)."

BRANCH="$(git rev-parse --abbrev-ref HEAD)"
[ "$BRANCH" = "main" ] || die "Dang o nhanh '$BRANCH', phai la 'main'. May chu chay code tu nhanh main (docs/FE-INTEGRATION.md)."

DIRTY="$(git status --porcelain --untracked-files=no)"
[ -z "$DIRTY" ] || die "Thu muc lam viec co file da sua chua commit:
$DIRTY
Commit hoac stash truoc khi deploy."

# Volume postgres mat nghia la co ai da chay 'down -v' -> du lieu da bay.
# Tuyet doi KHONG tu tao lai volume trong file nay.
docker volume inspect "$PG_VOLUME" >/dev/null 2>&1 \
  || die "Khong tim thay volume '$PG_VOLUME'. Database co the da bi xo・・ｽ｡.
KHONG chay 'docker compose down -v' tren may chay that. Hay khoi phuc tu ban sao luu."

# "|| true" o cuoi hai phep gan duoi day la bat buoc, khong phai thua. Voi "set -e" va
# "pipefail", khi $BACKUP_DIR chua ton tai thi df that bai -> ca duong ong that bai -> phep
# gan mang chinh ma loi do -> script chet NGAY TAI DAY va khong bao gio chay toi nhanh mkdir
# ben duoi. Trieu chung: deploy dung o buoc "kiem tra" ma khong in ra dong nao.
AVAIL_GB="$(df -BG --output=avail "$BACKUP_DIR" 2>/dev/null | tail -1 | tr -dc '0-9' || true)"
if [ -z "$AVAIL_GB" ]; then
  mkdir -p "$BACKUP_DIR" 2>/dev/null || die "Khong tao duoc thu muc sao luu $BACKUP_DIR. Dat BACKUP_DIR=/duong/dan/khac."
  AVAIL_GB="$(df -BG --output=avail "$BACKUP_DIR" | tail -1 | tr -dc '0-9' || true)"
fi
[ "${AVAIL_GB:-0}" -ge 5 ] || die "Con ${AVAIL_GB}GB o dia $BACKUP_DIR, can it nhat 5GB cho sao l・・ｽｰu + image Docker."

# .env la file du lieu, KHONG source (tranh no bi thuc thi nhu lenh shell).
# Doc bang grep/cut; gia tri co dau nhay hay # deu khong lam sai cau hinh.
read_env() {
  sed -n "s/^$1=//p" deploy/.env | tail -1 | sed -e 's/^["'\'']//' -e 's/["'\'']$//'
}
API_DOMAIN="$(read_env API_DOMAIN)"
[ -n "$API_DOMAIN" ] || die "deploy/.env khong co API_DOMAIN."
BASE_URL="https://$API_DOMAIN"

OLD_SHA="$(git rev-parse HEAD)"

say "Kiem tra OK 遯ｶ繝ｻnhanh $BRANCH, commit $OLD_SHA, domain $API_DOMAIN"

# Cho postgres san sang truoc khi bat dau dung psql (ngay sau khoi dong may
# hoac vua restart container thi may con thong bao chua san).
STEP="doi database"
for _ in $(seq 1 30); do
  docker exec csl-postgres pg_isready -U "$PG_USER" -d "$PG_DB" >/dev/null 2>&1 && break
  sleep 1
done
docker exec csl-postgres pg_isready -U "$PG_USER" -d "$PG_DB" >/dev/null 2>&1 \
  || die "Postgres khong san sau 30 giay."

# -----------------------------------------------------------------------------
# 2. SAO L・・ｽｯU 遯ｶ繝ｻbat buoc. Dump rong hoac hong thi dung luon, khong deploy tiep.
# -----------------------------------------------------------------------------
STEP="sao luu"
STAMP="$(date +%F-%H%M%S)"
mkdir -p "$BACKUP_DIR"

say "Sao luu database -> $BACKUP_DIR/backup-$STAMP.sql.gz"
docker exec csl-postgres pg_dump -U "$PG_USER" "$PG_DB" | gzip > "$BACKUP_DIR/backup-$STAMP.sql.gz"
# gzip -t: chung minh file gzip doc duoc. Dump that bai ma van bao "thanh cong"
# thi khong nguoi nao phat hien ra den khi can khoi phuc.
gzip -t "$BACKUP_DIR/backup-$STAMP.sql.gz" \
  || die "Ban sao luu gzip hong. KHONG deploy tiep."
[ -s "$BACKUP_DIR/backup-$STAMP.sql.gz" ] || die "Ban sao luu rong. KHONG deploy tiep."

# File am thanh la du lieu nghien cuu, khong tao lai duoc: sao luu volume MinIO.
# Dung chay thay the se ghi rac -> dung MinIO trong luc tar.
say "Sao luu volume MinIO -> $BACKUP_DIR/minio-$STAMP.tgz"
"${COMPOSE[@]}" stop minio >/dev/null
minio_backup_ok=0
if docker run --rm -v "$MINIO_VOLUME":/data -v "$BACKUP_DIR":/backup \
  alpine tar czf "/backup/minio-$STAMP.tgz" -C /data . >/dev/null; then
    minio_backup_ok=1
fi
"${COMPOSE[@]}" start minio >/dev/null
[ "$minio_backup_ok" -eq 1 ] || die "Sao luu volume MinIO that bai. KHONG deploy tiep."
[ -s "$BACKUP_DIR/minio-$STAMP.tgz" ] || die "Ban sao luu MinIO rong. KHONG deploy tiep."
say "Giu $KEEP_BACKUPS ban sao luu gan nhat"
ls -1t "$BACKUP_DIR"/*.sql.gz 2>/dev/null | tail -n +$((KEEP_BACKUPS + 1)) | while read -r old; do rm -f "$old"; done
ls -1t "$BACKUP_DIR"/*.tgz   2>/dev/null | tail -n +$((KEEP_BACKUPS + 1)) | while read -r old; do rm -f "$old"; done

# -----------------------------------------------------------------------------
# 3. K・・｢・CODE 遯ｶ繝ｻsau khi ・・ｦ･・｣ sao l・・ｽｰu
# -----------------------------------------------------------------------------
STEP="keo code"
say "git pull --ff-only"
# --ff-only: kh・・ｽｴng bao gi逶ｻ繝ｻt陂ｯ・｡o merge commit tr・・ｽｪn m・・ｽ｡y ch逶ｻ・ｧ.
git pull --ff-only
NEW_SHA="$(git rev-parse HEAD)"
[ "$NEW_SHA" != "$OLD_SHA" ] && say "Code moi: $OLD_SHA -> $NEW_SHA" || say "Khong co code moi."

# -----------------------------------------------------------------------------
# 4. MIGRATION 遯ｶ繝ｻch逶ｻ繝ｻch陂ｯ・｡y file ch・・ｽｰa c・・ｽｳ trong s逶ｻ繝ｻschema_migration
# -----------------------------------------------------------------------------
STEP="migration"

psql_q() { docker exec -i csl-postgres psql -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 -qtAX "$@"; }

say "Chuan bi s逶ｻ繝ｻschema_migration"
psql_q <<'SQL'
CREATE TABLE IF NOT EXISTS schema_migration (
    version      TEXT PRIMARY KEY,
    checksum     TEXT        NOT NULL,
    applied_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    duration_ms  INTEGER     NOT NULL
);
SQL

APPLIED_COUNT=0
if [ -d "$MIGRATIONS_DIR" ]; then
  # File dang t・・ｽｪn V<n>__<ten>.sql. File __verify_ l・・｣ｰ kiem chung, KHONG chay lai.
  # Dung mang + for, KHONG `while read < <(...)`: psql trong vong lap doc stdin
  # va nuot mat danh sach file con lai (da lam sot V18/V19/V20 ngay 27-28/10).
  mapfile -t migration_files < <(printf '%s\n' "$MIGRATIONS_DIR"/V*.sql | sort -V)
  for f in "${migration_files[@]}"; do
    [ -e "$f" ] || continue
    base="$(basename "$f")"
    case "$base" in *__verify_*) continue ;; esac

    version="${base%%__*}"
    sum="$(sha256sum "$f" | cut -d' ' -f1)"

    row="$(psql_q -c "SELECT checksum FROM schema_migration WHERE version = '$version'")"
    if [ -n "$row" ]; then
      if [ "$row" != "$sum" ]; then
        # Khong dung: V2 da bi sua sau khi ap (6eeeb3a), dung co chu ky cu
        # khac la chuyen binh thuong. Canh bao de biet, khong dung lai.
        warn "Migration $version da sua sau khi ap (checksum khac). Van coi la da chay."
      fi
      continue
    fi

    say "Ap migration $version ($base)"
    t0="$(date +%s%3N)"
    # File migration tu viet BEGIN/COMMIT nen psql o -v ON_ERROR_STOP=1 thi
    # fail -> rollback toan bo thay doi cua file do.
    docker exec -i csl-postgres psql -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 < "$f"
    t1="$(date +%s%3N)"
    # Chi ghi vao s逶ｻ繝ｻkhi migration ・・ｦ･・｣ xong. Fail o tren -> trap ERR -> dung,
    # va file do se chay lai o lan deploy sau.
    psql_q -c "INSERT INTO schema_migration (version, checksum, duration_ms) VALUES ('$version', '$sum', $((t1 - t0)))"
    APPLIED_COUNT=$((APPLIED_COUNT + 1))
  done
fi
[ "$APPLIED_COUNT" -gt 0 ] && say "Da ap $APPLIED_COUNT migration." || say "Khong co migration moi."

# -----------------------------------------------------------------------------
# 5. D逶ｻ・ｰNG L陂ｯ・ｰI CONTAINER
# -----------------------------------------------------------------------------
STEP="dung container"
say "docker compose up -d --build"
"${COMPOSE[@]}" up -d --build

# -----------------------------------------------------------------------------
# 6. KI逶ｻ繝ｻ TRA 遯ｶ繝ｻAPI phai tra /health truoc khi coi la xong
# -----------------------------------------------------------------------------
STEP="kiem tra API"
say "Cho API san sang ( toi da $((HEALTH_MAX_TRIES * HEALTH_INTERVAL)) giay )"
ok=0
for i in $(seq 1 "$HEALTH_MAX_TRIES"); do
  if curl -fsS --max-time 5 "$BASE_URL/health" >/dev/null 2>&1; then ok=1; break; fi
  sleep "$HEALTH_INTERVAL"
done
[ "$ok" -eq 1 ] || die "API khong tra /health sau $((HEALTH_MAX_TRIES * HEALTH_INTERVAL)) giay. Xem lenh rollback o tren."

say "Kiem tra dang nhap va doc du lieu"
SEED_PASSWORD="$(read_env SEED_PASSWORD)"
if [ -z "$SEED_PASSWORD" ]; then
  # Ban chay th陂ｯ・ｭt bo trong SEED_PASSWORD (deploy/README.md) -> khong co tai khoan mau
  # de thu. Chi kiem tra /health o day, n・・ｽｳi ro ra cho khong phai loi.
  warn "SEED_PASSWORD rong -> bo qua buoc dang nhap. API van tra /health OK."
else
  # Mat khau truyen qua STDIN chu khong phai -d: trong truong doi so lenh thi
  # moi nguoi tren may deu thay duoc qua `ps aux`. Khong ghi ra log.
  # -w tach HTTP status ra dong cuoi: curl -f truoc day nuot mat status nen loi
  # 500 (API/DB) bi bao nham thanh sai SEED_PASSWORD.
  resp="$(printf '{"email":"admin@codeswitchlabel.local","password":"%s"}' "$SEED_PASSWORD" \
            | curl -sS --max-time 10 -H 'Content-Type: application/json' --data-binary @- \
                   -w '\n%{http_code}' "$BASE_URL/api/auth/login" 2>/dev/null || true)"
  code="${resp##*$'\n'}"
  body="${resp%$'\n'*}"
  token="$(printf '%s' "$body" | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4 || true)"
  [ -n "$token" ] || die "Dang nhap that bai (HTTP ${code:-?}). 401/400: kiem tra SEED_PASSWORD; 5xx: loi API/DB, xem 'logs api'."
  # Token cung truyen qua STDIN (curl -K -) de khong lo ra truong doi so lenh.
  printf 'header = "Authorization: Bearer %s"\n' "$token" \
    | curl -fsS --max-time 10 -K - "$BASE_URL/api/campaigns" >/dev/null \
    || die "GET /api/campaigns that bai."
fi

git rev-parse HEAD > deploy/.deployed-sha

trap - ERR
say "XONG 遯ｶ繝ｻdang chay $NEW_SHA"
printf 'Kiem nhan nhanh:\n'
printf '  curl -s %s/health\n' "$BASE_URL"
printf '  curl -s %s/swagger/v1/swagger.json | head -c 200\n' "$BASE_URL"
printf '  git -C %s diff HEAD~1 --stat backend/  # thay doi moi nhat\n' "$ROOT"
