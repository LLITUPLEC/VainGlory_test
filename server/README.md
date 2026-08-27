# Ashfold server (Nakama + Postgres + Go)

Стек как у старого проекта на VPS: **тот же** `docker compose up -d`, Postgres volume, ключи, порты на `192.168.9.24`.  
Разница только в runtime-модулях: **Go → `modules/backend.so`**, а не `.lua`.

Nakama: **3.22.0** (как у вас раньше).

---

## Lua vs Go — алгоритм

| | Старый (Lua) | Ashfold (Go) |
|---|---|---|
| Файлы в `modules/` | `*.lua` | `backend.so` |
| Сборка | не нужна | **один раз** `./build-plugin.sh` (или после правок Go) |
| Запуск | `docker compose up -d` | то же самое |
| Volume | `./:/nakama/data` | то же |

Старые `.lua` из другого проекта из `modules/` лучше убрать, чтобы не мешали.

---

## Команды на VPS

Подготовьте каталог (например `~/nakama` или `~/ashfold`), положите туда содержимое `server/`:

```bash
cd ~/nakama   # или куда скопировали server/

# 1) Собрать Go-плагин (нужен Docker; Go на хосте не обязателен)
chmod +x build-plugin.sh
./build-plugin.sh
# должно появиться: modules/backend.so  (если снова ошибка — пришлите вывод)

# 2) Если старые контейнеры ещё крутятся — остановить
docker compose down

# 3) Поднять как раньше
docker compose up -d

# 4) Смотреть логи — обязательно: "Ashfold backend init" и modules count >= 1
docker compose logs -f nakama
```

Проверки:

```bash
docker compose ps
curl -s http://192.168.9.24:7350/
# Console: http://192.168.9.24:7351  (Prokrust / как в compose)
```

После правок Go — снова `./build-plugin.sh` и:

```bash
docker compose restart nakama
```

---

## Если Unity: «A task was canceled»

С ПК проверьте (PowerShell):

```powershell
Test-NetConnection api.prokrust-play.ru -Port 443
```

Если `TcpTestSucceeded : False` — проблема **не в коде**, а в reverse-proxy / firewall на VPS.
Nakama слушает только `192.168.9.24:7350`; снаружи нужен nginx/caddy на `:443`.

На VPS:

```bash
# слушает ли кто-то 443?
ss -tlnp | grep -E ':443|:7350'

# nginx / caddy жив?
systemctl status nginx
# или
systemctl status caddy
# или docker: docker ps | grep -iE 'nginx|caddy|traefik'

# локально до Nakama (должно ответить)
curl -sS -m 3 -u 'gDNVymCHsgbFr6QL4ENkImtds7Bu3T7bi1TG9QUDE0U=:' \
  http://192.168.9.24:7350/v2/healthcheck
```

Почините прокси (как в старом ProKrust), затем снова Play Mode.

---

## RPC

| Id | Назначение |
|----|------------|
| `ashfold_health` | проверка модуля |
| `ashfold_create_debug_match` | создать матч `ashfold_3v3` |
