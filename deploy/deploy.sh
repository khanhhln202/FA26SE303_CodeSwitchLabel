#!/usr/bin/env bash
# =============================================================================
# Cập nhật API đang chạy trên máy chủ — dùng thay cho quy trình gõ tay trong
# deploy/README.md. Chạy từ thư mục gốc của repo trên máy chủ:
#
#     ./deploy/deploy.sh
#
# Thứ tự: kiểm tra trước -> sao lưu -> kéo code -> nếu có migration mới thì
# dựng image, dừng API, sao lưu database lần nữa -> áp migration -> dựng lại
# container -> kiểm tra. Không bước nào tự ý đụng dữ liệu thật nếu bước trước
# chưa thành công.
#
# MIGRATION VÀ API KHÔNG CHẠY CÙNG LÚC: migration xoá cột/đổi kiểu (như V27 bỏ
# enum script_domain) làm code cũ hỏng ngay khi commit. Vì vậy khi có file
# migration mới, script dựng image trước (API cũ vẫn phục vụ), dừng API, rồi
# mới áp migration. Không cần viết migration theo kiểu expand/contract qua
# nhiều lần deploy; đổi lại có downtime ngắn mỗi lần có migration.
#
# Script KHÔNG BAO GIỜ tự phục hồi database. Phục hồi từ bản sao lưu tạo
# vài phút trước sẽ xoá mất dữ liệu thật ghi trong khoảng đó. Khi hỏng, script
# in ra đúng câu lệnh cần chạy rồi dừng, để người trực quyết định.
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

# Lệnh hoàn tác. In ra khi deploy hỏng — nhưng KHÔNG tự chạy: phục hồi bản
# sao lưu sẽ xoá mất dữ liệu thật ghi sau thời điểm sao lưu.
print_rollback() {
  cat >&2 <<ROLLBACK

--- Ban can CANH TAY LAM MOT TRONG CAC LENH SAU ---

1. Loi LUC DANG CHAY API (container moi khong len):
     docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env logs --tail 100 api

2. Neu migration da commit ma API moi khong len: phuc hoi database tu ban
   pre-migration (tao luc API da dung, nen khong mat du lieu da ghi), ROI MOI
   lui code — code cu KHONG chay duoc voi schema moi:
     ls -lh $BACKUP_DIR
     gunzip -c $BACKUP_DIR/pre-migration-<...>.sql.gz | docker exec -i csl-postgres psql -U $PG_USER -d $PG_DB -v ON_ERROR_STOP=1
     git checkout ${OLD_SHA:-<sha cu>}
     docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env up -d --build
     git checkout main && git reset --hard origin/main

3. Neu khong co file pre-migration (script dung truoc buoc do), dung
   backup-<...>.sql.gz. Ban nay cu hon — LAM MAT du lieu ghi sau thoi diem
   sao luu:
     ls -lh $BACKUP_DIR

4. Neu MinIO dang dung (sau backup that bai):
     docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env start minio

ROLLBACK
}

# API da dung nhung chua migration nao commit: luoc do van la ban cu, bat lai
# container cu la an toan. Khong dung toi database.
restart_old_api() {
  if [ "${API_STOPPED:-0}" = 1 ] && [ "${SCHEMA_CHANGED:-0}" -eq 0 ]; then
    warn "Chua migration nao commit — bat lai API cu."
    "${COMPOSE[@]}" start api >/dev/null 2>&1 \
      || warn "Khong bat lai duoc API cu. Xem muc 1 ben duoi."
  fi
}

# `die` tự thoát nên KHÔNG kích hoạt trap ERR — phải in lệnh hoàn tác ở đây,
# nếu không thì hỏng ở bước kiểm tra API sẽ không có hướng dẫn nào.
die()  { printf '\033[1;31m[DUNG] %s\033[0m\n' "$*" >&2; restart_old_api; print_rollback; exit 1; }

# Lỗi bất kỳ của lệnh -> in lệnh hoàn tác rồi dừng. Không tự phục hồi.
on_error() {
  local code=$?
  printf '\n\033[1;31m[LOI] Deploy dung giua chung (buoc %s).\033[0m\n' "${STEP:-?}" >&2
  restart_old_api
  print_rollback
  exit "$code"
}
trap on_error ERR

# -----------------------------------------------------------------------------
# 1. KIỂM TRA TRƯỚC — thất bại ở đây thì chưa đụng gì cả
# -----------------------------------------------------------------------------
STEP="kiem tra"

[ -f deploy/.env ] || die "Khong thay deploy/.env. Copy tu deploy/.env.example va dien gia tri that."

command -v curl >/dev/null || die "May chu thieu curl."
docker compose version >/dev/null 2>&1 || die "May chu thieu docker compose (v2)."

# Service asr nap model bang cach doc ca khoi float16 1,5 GB roi moi nen xuong int8,
# nen co mot dinh RAM nhat thoi vuot tran container. Khong co swap thi dinh do lam
# container bi giet va AI duyet hong, trong khi luc chay on dinh lai thua bo nho.
if [ "$(awk '/^SwapTotal:/{print $2}' /proc/meminfo 2>/dev/null || echo 0)" -lt 1048576 ]; then
  warn "May chu co duoi 1 GB swap. Service asr se bi giet luc nap model.
     Tao swap:  sudo fallocate -l 4G /swapfile && sudo chmod 600 /swapfile \
                && sudo mkswap /swapfile && sudo swapon /swapfile"
fi

BRANCH="$(git rev-parse --abbrev-ref HEAD)"
[ "$BRANCH" = "main" ] || die "Dang o nhanh '$BRANCH', phai la 'main'. May chu chay code tu nhanh main (docs/FE-INTEGRATION.md)."

DIRTY="$(git status --porcelain --untracked-files=no)"
[ -z "$DIRTY" ] || die "Thu muc lam viec co file da sua chua commit:
$DIRTY
Commit hoac stash truoc khi deploy."

# Volume postgres mat nghia la co ai da chay 'down -v' -> du lieu da bay.
# Tuyet doi KHONG tu tao lai volume trong file nay.
docker volume inspect "$PG_VOLUME" >/dev/null 2>&1 \
  || die "Khong tim thay volume '$PG_VOLUME'. Database co the da bi xoá.
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
[ "${AVAIL_GB:-0}" -ge 5 ] || die "Con ${AVAIL_GB}GB o dia $BACKUP_DIR, can it nhat 5GB cho sao lưu + image Docker."

# .env la file du lieu, KHONG source (tranh no bi thuc thi nhu lenh shell).
# Doc bang grep/cut; gia tri co dau nhay hay # deu khong lam sai cau hinh.
read_env() {
  sed -n "s/^$1=//p" deploy/.env | tail -1 | sed -e 's/^["'\'']//' -e 's/["'\'']$//'
}
API_DOMAIN="$(read_env API_DOMAIN)"
[ -n "$API_DOMAIN" ] || die "deploy/.env khong co API_DOMAIN."
BASE_URL="https://$API_DOMAIN"

OLD_SHA="$(git rev-parse HEAD)"

say "Kiem tra OK — nhanh $BRANCH, commit $OLD_SHA, domain $API_DOMAIN"

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
# 2. SAO LƯU — bat buoc. Dump rong hoac hong thi dung luon, khong deploy tiep.
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
# 3. KÉO CODE — sau khi đã sao lưu
# -----------------------------------------------------------------------------
STEP="keo code"
say "git pull --ff-only"
# --ff-only: không bao giờ tạo merge commit trên máy chủ.
git pull --ff-only
NEW_SHA="$(git rev-parse HEAD)"
[ "$NEW_SHA" != "$OLD_SHA" ] && say "Code moi: $OLD_SHA -> $NEW_SHA" || say "Khong co code moi."

# -----------------------------------------------------------------------------
# 4. MIGRATION — chỉ chạy file chưa có trong sổ schema_migration
#
# Khi có file mới: dựng image trước (API cũ vẫn phục vụ), dừng API, sao lưu
# database lần nữa, rồi mới áp. Code cũ không bao giờ chạy trên lược đồ mới.
# -----------------------------------------------------------------------------
STEP="migration"

psql_q() { docker exec -i csl-postgres psql -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 -qtAX "$@"; }

say "Chuan bi sổ schema_migration"
psql_q <<'SQL'
CREATE TABLE IF NOT EXISTS schema_migration (
    version      TEXT PRIMARY KEY,
    checksum     TEXT        NOT NULL,
    applied_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    duration_ms  INTEGER     NOT NULL
);
SQL

# Tìm file chưa áp trước khi quyết định dừng API. File __verify_ là kiểm
# chứng, KHÔNG chạy lại.
pending_files=()
if [ -d "$MIGRATIONS_DIR" ]; then
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
    pending_files+=("$f")
  done
fi

# Script đang chạy là bản bash đã nạp TRƯỚC `git pull`. Nếu chính deploy.sh
# vừa đổi trong lần kéo này thì nó không chạy logic mới. Code mới đã nằm
# trên đĩa, nên dừng và yêu cầu chạy lại thay vì áp migration bằng logic cũ.
if [ "${#pending_files[@]}" -gt 0 ] && [ "$NEW_SHA" != "$OLD_SHA" ] \
   && ! git diff --quiet "$OLD_SHA" "$NEW_SHA" -- deploy/deploy.sh; then
  warn "Lần kéo này vừa đổi deploy/deploy.sh vừa có migration mới."
  warn "Tiến trình bash đang chạy vẫn là bản CŨ (nạp trước git pull)."
  warn "Chạy lại ./deploy/deploy.sh để dùng logic mới — lúc đó migration mới được áp an toàn."
  exit 1
fi

if [ "${#pending_files[@]}" -gt 0 ]; then
  say "Có ${#pending_files[@]} migration mới -> dựng image, dừng API, rồi mới áp"

  STEP="dung image"
  # Dựng trước khi dừng: build hỏng thì API cũ vẫn đang phục vụ, chưa đụng gì.
  "${COMPOSE[@]}" build

  STEP="dung API"
  API_STOPPED=1
  "${COMPOSE[@]}" stop -t 30 api >/dev/null

  # Sao lưu lúc API đã dừng: phục hồi điểm này không mất dữ liệu người dùng
  # đã ghi. Bản sao lưu lúc đầu được tạo trước khi build nên có thể cũ vài phút.
  STEP="sao luu migration"
  say "Sao lưu database trước migration -> $BACKUP_DIR/pre-migration-$STAMP.sql.gz"
  docker exec csl-postgres pg_dump -U "$PG_USER" "$PG_DB" | gzip > "$BACKUP_DIR/pre-migration-$STAMP.sql.gz"
  gzip -t "$BACKUP_DIR/pre-migration-$STAMP.sql.gz" \
    || die "Bản sao lưu trước migration hỏng. KHÔNG áp migration."
  [ -s "$BACKUP_DIR/pre-migration-$STAMP.sql.gz" ] \
    || die "Bản sao lưu trước migration rỗng. KHÔNG áp migration."

  STEP="migration"
fi

SCHEMA_CHANGED=0
APPLIED_COUNT=0
for f in "${pending_files[@]}"; do
  base="$(basename "$f")"
  version="${base%%__*}"
  sum="$(sha256sum "$f" | cut -d' ' -f1)"

  say "Ap migration $version ($base)"
  t0="$(date +%s%3N)"
  # File migration tu viet BEGIN/COMMIT nen psql o -v ON_ERROR_STOP=1 thi
  # fail -> rollback toan bo thay doi cua file do.
  docker exec -i csl-postgres psql -U "$PG_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 < "$f"
  SCHEMA_CHANGED=1
  t1="$(date +%s%3N)"
  # Chi ghi vao sổ khi migration đã xong. Fail o tren -> trap ERR -> dung,
  # va file do se chay lai o lan deploy sau.
  psql_q -c "INSERT INTO schema_migration (version, checksum, duration_ms) VALUES ('$version', '$sum', $((t1 - t0)))"
  APPLIED_COUNT=$((APPLIED_COUNT + 1))
done
[ "$APPLIED_COUNT" -gt 0 ] && say "Da ap $APPLIED_COUNT migration." || say "Khong co migration moi."

# -----------------------------------------------------------------------------
# 5. DỰNG LẠI CONTAINER
# -----------------------------------------------------------------------------
STEP="dung container"
say "docker compose up -d --build"
"${COMPOSE[@]}" up -d --build

# -----------------------------------------------------------------------------
# 5b. NAP MODEL NHAN DANG GIONG NOI
#
# Service asr KHONG tu tai model khi co request phien am den — no tra 404. Phai goi
# POST /v1/models/{id} truoc. Buoc nay lam viec do, va lam idempotent: co roi thi bo qua.
# Khong co buoc nay thi moi lan volume asr-models mat la AI duyet hong lang le.
# -----------------------------------------------------------------------------
STEP="nap model ASR"
ASR_MODEL_WANTED="$(read_env ASR_MODEL)"

if [ -z "$ASR_MODEL_WANTED" ]; then
  say "ASR_MODEL rong -> bo qua buoc nap model."
elif ! "${COMPOSE[@]}" ps --services --status running 2>/dev/null | grep -qx asr; then
  warn "Service asr khong chay -> bo qua buoc nap model. AI duyet se bao asr_failed."
else
  say "Kiem tra model $ASR_MODEL_WANTED (lan dau phai tai ~1,5 GB)"
  # Hong o day KHONG duoc lam do ca lan deploy: phan con lai cua he van chay duoc,
  # chi rieng AI duyet la chua dung duoc.
  if "${COMPOSE[@]}" exec -T asr python - "$ASR_MODEL_WANTED" <<'PY'
import json, sys, urllib.request
model = sys.argv[1]
base = "http://localhost:8000"
have = json.load(urllib.request.urlopen(base + "/v1/models", timeout=30))
if model in {m["id"] for m in have.get("data", [])}:
    print("Model da co san.")
    sys.exit(0)
print("Chua co, dang tai...")
urllib.request.urlopen(
    urllib.request.Request(base + "/v1/models/" + model, method="POST"), timeout=3600)
print("Tai xong.")
PY
  then
    say "Model ASR san sang."
  else
    warn "Nap model ASR that bai. He thong van chay, rieng AI duyet se bao asr_failed.
     Thu tay:  ${COMPOSE[*]} exec asr python -c \
       \"import urllib.request as u;u.urlopen(u.Request('http://localhost:8000/v1/models/$ASR_MODEL_WANTED',method='POST'),timeout=3600)\""
  fi
fi

# -----------------------------------------------------------------------------
# 6. KIỂM TRA — API phai tra /health truoc khi coi la xong
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
  # Ban chay thật bo trong SEED_PASSWORD (deploy/README.md) -> khong co tai khoan mau
  # de thu. Chi kiem tra /health o day, nói ro ra cho khong phai loi.
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
  # /api/auth/me chu khong phai /api/waves: route nay khong doi ten theo domain,
  # nen script dang chay (ban cu, nap truoc khi git pull) khong bao sai khi ten doi.
  printf 'header = "Authorization: Bearer %s"\n' "$token" \
    | curl -fsS --max-time 10 -K - "$BASE_URL/api/auth/me" >/dev/null \
    || die "GET /api/auth/me that bai."
fi

git rev-parse HEAD > deploy/.deployed-sha

trap - ERR
say "XONG — dang chay $NEW_SHA"
printf 'Kiem nhan nhanh:\n'
printf '  curl -s %s/health\n' "$BASE_URL"
printf '  curl -s %s/swagger/v1/swagger.json | head -c 200\n' "$BASE_URL"
printf '  git -C %s diff HEAD~1 --stat backend/  # thay doi moi nhat\n' "$ROOT"
