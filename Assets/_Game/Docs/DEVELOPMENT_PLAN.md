# Ashfold — план разработки (3v3 в духе Vainglory)

Рабочее название **Ashfold** (не использовать IP Vainglory: имена героев, Halcyon Fold, предметы SEMC).

**Стек (без Photon Cloud):** Unity 6 URP · **Nakama self-host на VPS** = аккаунт, очередь, награды **и** авторитетный бой (**Go** match loop, не Lua).  
Код сервера: папка `server/` (Docker Compose + Postgres + Go plugin).  
Запасной путь для боя: **FishNet** — только если Go-симуляция упрётся.

**Почему не Photon:** dashboard.photonengine.com недоступен (РКН). Fusion/Quantum AppId без дашборда не получить. Self-host Photon Server тоже упирается в лицензию/аккаунт Photon.

**Контент v1:** 3 героя · 6 предметов · 1 турель + кристалл на сторону · мид + лес.

Каждый этап заканчивается **проверкой**. Не перескакивать через непройденный этап.

### Где мы сейчас (2026-08-27)

Пройдено: **этап 0**, офлайн **2–4**, полировка **6.1–6.2** и **6.6** (en/ru), сервер **5.0–5.3**, аккаунт **1.1–1.8**.  
**Только что:** очередь Nakama (2 игрока → матч `ashfold_3v3`), SOLO vs боты. Драфт пиков ещё не общий.

**Дальше:** пересобрать плагин на VPS, проверить двумя клиентами. Затем **5.4A** — Go MatchLoop (движение/HP).

---

## Поток клиента (как в Vainglory)

```
Splash → Login/Guest → Hall (PLAY / Heroes / Shop)
      → Mode (Casual 3v3) → Queue → Match Found
      → Draft (пик героя) → Loading
      → Battle → Victory/Defeat + таблица статов → Rewards → Hall
```

Боевой сокет открывается только после Match Found, закрывается после Results. В Hall — только мета-API Nakama.

---

## Сетевые варианты (выбор)

| Вариант | Плюсы | Минусы | Вердикт |
|---|---|---|---|
| **A. Nakama authoritative (Go)** | Один сервер на VPS, матчмейкинг+бой, тик 10 Гц | Бой на Go (перенос логики с Unity) | **Выбран** |
| **B. FishNet + Nakama** | Бой остаётся в Unity C#, GitHub/OpenUPM | Нужен host или headless Unity; 4 ГБ VPS тесно на dedicated | Запас, если Go-симуляция упрётся |
| **C. Mirror + Nakama** | Проще старт, MIT | Слабее предикт, чем FishNet | Ок для прототипа 2 игроков |
| Photon Fusion/Quantum | Готовый netcode | **Дашборд заблокирован** | Снят с плана |

VPN/прокси ради Photon возможны для разработчика, но игрокам в РФ это не раздавать. В плане не опираемся на Photon.

---

## Этап 0 — каркас клиента ✅

| ID | Что сделать | Как проверить |
|---|---|---|
| **0.1–0.6** | Boot, гость, Hall, GameSession | Play Mode с Boot |

---

## Этап 1 — аккаунт (Nakama)

| ID | Что сделать | Как проверить |
|---|---|---|
| **1.1** | Интерфейс `IAuthService` (Dev есть) | ✅ |
| **1.2** | Nakama+Postgres на VPS (`server/`) | ✅ новый VPS: `~/ashfold`, `backend.so`, Caddy на `:443` |
| **1.3** | Device login (`NakamaAuthService`) | ✅ Guest → `Nakama authenticated` + `ashfold_health` |
| **1.4** | Сессия на диск, автологин | ✅ токен + refresh; Boot → Restore / silent device |
| **1.5** | Профиль в Hall | ✅ имя/lvl/Essence с GetAccount + Storage; метка SAVED если email |
| **1.6** | Ошибка сети на Splash | ✅ Retry + EMAIL / GUEST, понятные ошибки |
| **1.7** | Storage: открытые герои (3) | ✅ collection `progress` / key `meta`; v1 все трое открыты |
| **1.8** | Email + пароль (восстановление) | ✅ Hall → ACCOUNT → Link; новый девайс: Boot → EMAIL |

**Клиент → VPS:** `NakamaConfig.cs` — `UseServer=true`, `https` / `api.prokrust-play.ru` / `443`, ServerKey как в compose.  
Прокси: **Caddy** (`127.0.0.1:7350`), не NPM и не LAN `192.168.9.24`. Unity HTTP на IP:7350 блокирует insecure.  
Подробности деплоя: `server/README.md`.

---

## Этап 2 — Hall и путь в бой ✅ (очередь пока локальная)

| ID | Что | Статус |
|---|---|---|
| **2.1–2.9** | Heroes, Shop, PLAY, очередь-фейк, драфт, loading | Сделано |

Очередь Nakama — в этапе 5.

---

## Этап 3 — офлайн-бой ✅ (ядро)

| ID | Что | Статус |
|---|---|---|
| **3.1–3.18** | Карта, бой, крипы, турели, магазин, боты, Results-выход | Сделано |

---

## Этап 4 — результаты ✅

| ID | Что | Статус |
|---|---|---|
| **4.1–4.4** | Victory/Defeat, таблица, Essence | Сделано |
| **4.5** | Запись в Nakama Storage | ✅ Essence после Results → `progress/meta` |

---

## Этап 5 — сеть (Nakama), без Photon

Цель: 2 живых клиента в одном 3v3 (остальные — боты), потом 6 живых.

| ID | Что сделать | Как проверить |
|---|---|---|
| **5.0** | Путь **A** (Go) зафиксирован | ✅ `server/` |
| **5.1** | Docker Compose: Nakama + Postgres + Go plugin | ✅ на VPS: `./build-plugin.sh` + `docker compose up -d` (не `--build`) |
| **5.2** | Unity Nakama SDK, логин device | ✅ совпадает с 1.3 |
| **5.3** | Matchmaker 3v3 (или room create для теста 1v1) | ✅ очередь на 2 игрока → `ashfold_3v3`; SOLO = боты. Пики героев ещё не синхронны |
| **5.4A** | Go `MatchLoop`: движение, HP, автоатака (тик 10 Гц) | Одинаковый last-hit у обоих |
| **5.5A** | Крипы + турель + кристалл на сервере | Победа по кристаллу с сервера |
| **5.6A** | Клиент Unity = отображение снапшотов + локальный предикт героя | Нет «двойной истины» |
| **5.4B** *(альт.)* | FishNet Host: игрок-хост симулирует бой | 2 билда видят друг друга |
| **5.5B** | Пустые слоты = боты на хосте | 1v1+боты |
| **5.7** | Подключение только после Match Found | В Hall нет игрового сокета матча |
| **5.8** | Disconnect / Leave после Results | Матч гасится |
| **5.9** | Реконнект 30 с | Возврат в тот же match id |
| **5.10** | Запись результата → Storage / wallet Essence | Hall обновляет Essence |

На VPS 2c/4GB: путь **A** масштабируется на много лёгких матчей; путь **B** Host — только для тестов (телефон-хост = лаги у всех).

---

## Этап 6 — полировка 3v3

| ID | Что сделать | Как проверить |
|---|---|---|
| **6.1** | Кусты | ✅ Сделано |
| **6.2** | Мини-карта | ✅ Сделано |
| **6.3** | Пинг по карте | Союзник видит |
| **6.4** | Баланс 10 каток | Нет авто-вина одной роли |
| **6.5** | Туториал 60 с | Первый вход |
| **6.6** | Локализация UI: en/ru, глобус в Hall → ACCOUNT | ✅ `Loc` + PlayerPrefs; смена языка пересобирает сцену (сессия DDOL). Первый запуск: язык ОС. Проверка: ACCOUNT → глобус → Русский → кнопки Hall на русском |

---

## Этап 7 — социальное (Nakama)

Друзья, пати из Hall, чат пати. Боевой transport не трогать.

---

## Правило сети (обновлено)

| Где игрок | Nakama мета | Боевой матч (Nakama Match / FishNet) |
|---|---|---|
| Splash / Login / Hall / Shop | да | нет |
| Очередь | да | нет |
| Драфт + бой | да (мета) | **да** |
| Results → Hall | да | отключить |

Нет CCU Photon. Лимит = CPU/RAM вашего VPS и число нод Nakama.

---

## Инфра для этапа 5 (путь A) — стоит

1. ✅ VPS Ubuntu 26.04, Docker + Compose, UFW (`80`/`443`).
2. ✅ Образ `heroiclabs/nakama:3.22.0` + Postgres 12 в compose.
3. ✅ Unity package `com.heroiclabs.nakama-unity`.
4. ✅ Go-модуль: `server/` → `modules/backend.so` (RPC, matchmaker → `ashfold_3v3`, roster). После правок Go: `./build-plugin.sh` + `docker compose restart nakama`.
5. ✅ Публичный вход: A-запись `api.prokrust-play.ru` → `46.173.17.51`, Caddy + Let’s Encrypt.

До **5.4A** (Go бой) очередь Nakama на двоих уже в клиенте; на VPS нужна пересборка плагина.

FishNet (путь B): GitHub `FirstGearGames/FishNet` или OpenUPM — запас, без Photon.
