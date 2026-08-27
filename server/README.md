# Ashfold server (Nakama + Postgres + Go)

**Живой стенд (2026-08-27):** Beget VPS Ubuntu 26.04 · IP `46.173.17.51` · каталог `~/ashfold`.

| Слой | Что |
|---|---|
| Nakama | `3.22.0` + Go-плагин `modules/backend.so` |
| Postgres | контейнер `postgres:12.2-alpine`, БД `nakama` |
| Снаружи | **Caddy** + Let’s Encrypt: `https://api.prokrust-play.ru:443` → `127.0.0.1:7350` |
| Клиент Unity | `NakamaConfig`: `https` / `api.prokrust-play.ru` / `443` |
| Console | `http://46.173.17.51:7351` (логин как в `docker-compose.yml`) |

Прямой HTTP `:7350` с Unity **нельзя**: Player Settings режут insecure. Домен без Caddy на `:443` тоже не зайдёт.

Nakama слушает `0.0.0.0:7349-7351`. Старый LAN `192.168.9.24` и Nginx Proxy Manager **не используются**.

---

## Lua vs Go

| | Старый (Lua) | Ashfold (Go) |
|---|---|---|
| Файлы в `modules/` | `*.lua` | `backend.so` |
| Сборка | не нужна | `./build-plugin.sh` (после правок Go) |
| Запуск | `docker compose up -d` | то же |
| Volume | `./:/nakama/data` | то же |

Go на хосте не нужен: сборка в `heroiclabs/nakama-pluginbuilder:3.22.0`.  
Если `./build-plugin.sh: /bin/sh^M` — CRLF с Windows: `sed -i 's/\r$//' build-plugin.sh`. В репо стоит `server/.gitattributes` (`*.sh` → LF).

---

## Поднять с нуля (новый VPS)

1. Docker Engine + Compose plugin, `ufw`: `OpenSSH`, `80`, `443` (по желанию `7351` для Console).
2. Скопировать содержимое `server/` в `~/ashfold`. В `docker-compose.yml` порты Nakama: `0.0.0.0:7350:7350` и соседние, **не** старый LAN-IP.
3. Собрать плагин и поднять контейнеры:

```bash
cd ~/ashfold
chmod +x build-plugin.sh
./build-plugin.sh          # modules/backend.so
docker compose up -d
docker compose logs -f nakama
# в логе: "Ashfold backend init", modules count >= 1, "Startup done"
```

4. A-запись `api.prokrust-play.ru` → публичный IP VPS.
5. Caddy (`/etc/caddy/Caddyfile`) — **весь** дефолтный `:80 { file_server }` заменить на:

```caddy
{
	email your@email
}

api.prokrust-play.ru {
	reverse_proxy 127.0.0.1:7350
}
```

```bash
sudo ufw allow 80/tcp && sudo ufw allow 443/tcp
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

После правок Go:

```bash
./build-plugin.sh
docker compose restart nakama
```

---

## Проверки

```bash
# TLS + прокси живы → 401 (нет сессии), не таймаут
curl -sS -m 5 -o /dev/null -w "%{http_code}\n" https://api.prokrust-play.ru/v2/account

# Console
# http://46.173.17.51:7351
```

С ПК:

```powershell
Test-NetConnection api.prokrust-play.ru -Port 443
nslookup api.prokrust-play.ru
```

В Unity Play Mode: Guest → лог `Nakama authenticated` и `ashfold_health → … "ok":true`.

`GET /v2/healthcheck` у Nakama 3.22 **нет** (`Not Found`) — это не падение сервера.

`ERROR: Failed to extract ServerMetadata from context` — шум grpc-gateway (curl без gRPC-метаданных, сканы `:7350`). На логин/RPC не влияет. Чтобы меньше мусора снаружи: закрыть UFW `7349`/`7350`, оставить `80`/`443` (и `7351` при необходимости).

---

## Если Unity: timeout / «A task was canceled»

Обычно **не ServerKey**, а нет TLS на `:443` или DNS смотрит не на этот VPS.

```bash
ss -tlnp | grep -E ':443|:7350'
systemctl status caddy
```

---

## RPC

| Id | Назначение |
|----|------------|
| `ashfold_health` | проверка модуля |
| `ashfold_create_debug_match` | создать матч `ashfold_3v3` вручную |
| matchmaker | 2 игрока с `mode=casual_3v3` → `MatchCreate(ashfold_3v3)` |

После смены Go на VPS:

```bash
cd ~/ashfold
# скопируй новые main.go и match/, затем:
./build-plugin.sh
docker compose restart nakama
docker compose logs -f nakama
# в логе: "Ashfold registered ... matchmaker"
```

Два клиента: PLAY → CASUAL 3v3. Ждут друг друга, затем Match Found (имена живых в драфте). SOLO — офлайн vs боты. Бой пока локальный (этап 5.4A).
