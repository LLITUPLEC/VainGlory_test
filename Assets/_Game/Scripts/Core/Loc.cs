using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ashfold
{
    /// <summary>
    /// Локализация UI. Язык в PlayerPrefs; смена пересобирает текущую сцену
    /// (GameSession DDOL, сессия не сбрасывается).
    /// </summary>
    public static class Loc
    {
        public const string En = "en";
        public const string Ru = "ru";
        const string PrefsKey = "ashfold.lang";

        public static string Code { get; private set; } = En;

        public static readonly LangInfo[] Languages =
        {
            new LangInfo(En, "English"),
            new LangInfo(Ru, "Русский")
        };

        static Dictionary<string, string> _en;
        static Dictionary<string, string> _ru;
        static bool _reopenAccount;

        public readonly struct LangInfo
        {
            public readonly string Code;
            public readonly string NativeName;
            public LangInfo(string code, string nativeName)
            {
                Code = code;
                NativeName = nativeName;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Code = En;
            _en = null;
            _ru = null;
            _reopenAccount = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            EnsureTables();
            var saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(saved))
                saved = Application.systemLanguage == SystemLanguage.Russian ? Ru : En;
            Apply(saved, persist: false, reload: false, reopenAccount: false);
        }

        public static bool ConsumeReopenAccount()
        {
            var v = _reopenAccount;
            _reopenAccount = false;
            return v;
        }

        public static void Set(string code, bool reopenAccount = false)
        {
            var next = code == Ru ? Ru : En;
            if (next == Code)
                return;
            Apply(next, persist: true, reload: true, reopenAccount: reopenAccount);
        }

        static void Apply(string code, bool persist, bool reload, bool reopenAccount)
        {
            Code = code == Ru ? Ru : En;
            if (persist)
            {
                PlayerPrefs.SetString(PrefsKey, Code);
                PlayerPrefs.Save();
            }

            _reopenAccount = reopenAccount;
            if (reload)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid())
                    SceneManager.LoadScene(scene.name);
            }
        }

        public static string T(string key)
        {
            EnsureTables();
            var table = Code == Ru ? _ru : _en;
            if (table.TryGetValue(key, out var s))
                return s;
            if (_en.TryGetValue(key, out var fallback))
                return fallback;
            return key;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static string ModeLabel(string stored)
        {
            if (stored == "Casual 3v3")
                return T("mode.casual");
            return stored;
        }

        public static string MapLabel(string stored)
        {
            if (stored == "Ashfold Lane")
                return T("map.ashfold");
            return stored;
        }

        static void EnsureTables()
        {
            if (_en != null)
                return;
            _en = new Dictionary<string, string>(128);
            _ru = new Dictionary<string, string>(128);
            Add("boot.subtitle", "BATTLE FOR THE FOLD  ·  3v3", "БИТВА ЗА FOLD  ·  3v3");
            Add("boot.init", "Initializing…", "Инициализация…");
            Add("boot.init_client", "Initializing client…", "Запуск клиента…");
            Add("boot.connecting", "Connecting…", "Подключение…");
            Add("boot.awaiting", "Awaiting commander…", "Ожидание командира…");
            Add("boot.welcome_back", "Welcome back, {0}", "С возвращением, {0}");
            Add("boot.loading_roster", "Loading roster · 3 heroes, 6 items", "Загрузка состава · 3 героя, 6 предметов");
            Add("boot.restoring", "Restoring session…", "Восстановление сессии…");
            Add("boot.signing_in", "Signing in…", "Вход…");
            Add("boot.auth_failed_retry", "Auth failed — Retry or sign in", "Ошибка входа — повторите или войдите");
            Add("boot.signed_in", "Signed in as {0}", "Вошли как {0}");
            Add("boot.signing_nakama", "Signing in via Nakama…", "Вход через Nakama…");
            Add("boot.signing_email", "Signing in with email…", "Вход по email…");
            Add("boot.enter_fold", "ENTER THE FOLD", "ВОЙТИ В FOLD");
            Add("boot.kicked_device", "Signed in on another device — please log in again", "Вход с другого устройства — войдите снова");
            Add("boot.tap_enter", "TAP TO ENTER", "НАЖМИТЕ, ЧТОБЫ ВОЙТИ");
            Add("boot.email_hint", "Email sign-in  ·  account from another device", "Вход по email  ·  аккаунт с другого устройства");
            Add("boot.name_ph", "Commander name", "Имя командира");
            Add("boot.play_guest", "PLAY AS GUEST", "ИГРАТЬ ГОСТЕМ");
            Add("boot.email", "EMAIL", "EMAIL");
            Add("boot.retry", "RETRY", "ПОВТОРИТЬ");
            Add("boot.email_ph", "Email", "Email");
            Add("boot.pass_ph", "Password (min 8)", "Пароль (мин. 8)");
            Add("boot.sign_in", "SIGN IN", "ВОЙТИ");
            Add("boot.back", "BACK", "НАЗАД");
            Add("boot.err.timeout", "Network timeout — Retry", "Таймаут сети — повторите");
            Add("boot.err.credentials", "Wrong email or password", "Неверный email или пароль");
            Add("boot.err.linked", "Email already linked to another account", "Email уже привязан к другому аккаунту");
            Add("boot.err.generic", "Auth failed — Retry", "Ошибка входа — повторите");
            Add("boot.stage_nakama", "STAGE 1.x  ·  NAKAMA", "ЭТАП 1.x  ·  NAKAMA");
            Add("boot.stage_dev", "STAGE 0.2  ·  BOOT", "ЭТАП 0.2  ·  BOOT");
            Add("boot.footer", "NAKAMA SELF-HOST  ·  GO MATCH  ·  PROTOTYPE", "NAKAMA SELF-HOST  ·  GO MATCH  ·  ПРОТОТИП");

            Add("hall.guest", "Guest", "Гость");
            Add("hall.play", "PLAY", "ИГРАТЬ");
            Add("hall.heroes", "HEROES", "ГЕРОИ");
            Add("hall.shop", "SHOP", "МАГАЗИН");
            Add("hall.friends", "FRIENDS", "ДРУЗЬЯ");
            Add("hall.account", "ACCOUNT", "АККАУНТ");
            Add("hall.essence", "ESSENCE  {0}", "ЭССЕНЦИЯ  {0}");
            Add("hall.profile", "{0}   LVL {1}", "{0}   УР. {1}");
            Add("hall.saved", "  ·  SAVED", "  ·  СОХРАНЁН");
            Add("hall.stage_nakama", "HALL  ·  NAKAMA", "ХОЛЛ  ·  NAKAMA");
            Add("hall.stage_dev", "STAGE 2.1  ·  HALL", "ЭТАП 2.1  ·  ХОЛЛ");
            Add("hall.friends_toast", "Friends — stage 7 (Nakama)", "Друзья — этап 7 (Nakama)");

            Add("account.title", "ACCOUNT", "АККАУНТ");
            Add("account.close", "CLOSE", "ЗАКРЫТЬ");
            Add("account.email_none", "not linked", "не привязан");
            Add("account.info", "{0}  ·  LVL {1}\nEmail: {2}\nLink email to keep progress if you lose this device.", "{0}  ·  УР. {1}\nEmail: {2}\nПривяжите email, чтобы не потерять прогресс при смене устройства.");
            Add("account.tied", "Progress is tied to this email.\nOn a new phone: Boot → EMAIL → sign in.", "Прогресс привязан к этому email.\nНа новом телефоне: Boot → EMAIL → вход.");
            Add("account.need_nakama", "Email link needs Nakama (UseServer=true).", "Привязка email нужна Nakama (UseServer=true).");
            Add("account.link", "LINK EMAIL", "ПРИВЯЗАТЬ EMAIL");
            Add("account.sign_out", "SIGN OUT", "ВЫЙТИ");
            Add("account.linking", "Linking…", "Привязка…");
            Add("account.link_failed", "Link failed", "Не удалось привязать");
            Add("account.linked", "Linked · {0}", "Привязан · {0}");
            Add("lang.title", "LANGUAGE", "ЯЗЫК");
            Add("lang.hint", "Applies immediately.", "Применится сразу.");

            Add("catalog.heroes", "HEROES", "ГЕРОИ");
            Add("catalog.close", "CLOSE", "ЗАКРЫТЬ");
            Add("catalog.locked", "LOCKED", "ЗАКРЫТ");
            Add("catalog.shop", "SHOP  ·  COSMETICS STUB", "МАГАЗИН  ·  КОСМЕТИКА (ЗАГЛУШКА)");
            Add("catalog.in_match", "(in-match shop · 3.13)", "(магазин в бою · 3.13)");

            Add("role.tank", "TANK", "ТАНК");
            Add("role.carry", "CARRY", "КЕРРИ");
            Add("role.support", "SUPPORT", "САППОРТ");

            Add("hero.bastion.tagline", "Frontline steel", "Сталь переднего края");
            Add("hero.vesper.tagline", "Lane pressure", "Давление на линии");
            Add("hero.mira.tagline", "Keeps the fold", "Хранит Fold");
            Add("hero.bastion.skill", "Bulwark", "Оплот");
            Add("hero.vesper.skill", "Bolt", "Залп");
            Add("hero.mira.skill", "Mend", "Исцеление");

            Add("item.iron_edge.name", "Iron Edge", "Железный клинок");
            Add("item.storm_charm.name", "Storm Charm", "Штормовой талисман");
            Add("item.stoneplate.name", "Stoneplate", "Каменная броня");
            Add("item.wardcloak.name", "Wardcloak", "Плащ стража");
            Add("item.lifewell.name", "Lifewell", "Живой источник");
            Add("item.pulse_beacon.name", "Pulse Beacon", "Импульсный маяк");
            Add("item.iron_edge.effect", "+25 attack", "+25 урона");
            Add("item.storm_charm.effect", "+25% attack speed", "+25% скорости атаки");
            Add("item.stoneplate.effect", "+180 HP", "+180 HP");
            Add("item.wardcloak.effect", "+18% resist", "+18% сопротивления");
            Add("item.lifewell.effect", "+40% heal power", "+40% силы лечения");
            Add("item.pulse_beacon.effect", "+12% move speed", "+12% скорости");
            Add("item.branch.damage", "Damage", "Урон");
            Add("item.branch.defense", "Defense", "Защита");
            Add("item.branch.support", "Support", "Поддержка");

            Add("mode.title", "SELECT MODE", "ВЫБОР РЕЖИМА");
            Add("mode.casual_btn", "CASUAL  3v3", "ОБЫЧНЫЙ  3v3");
            Add("mode.solo_btn", "SOLO  ·  BOTS", "СОЛО  ·  БОТЫ");
            Add("mode.hint", "Lane + jungle  ·  one turret  ·  crystal\nOffline queue (DevAuth)", "Линия + лес  ·  одна турель  ·  кристалл\nОфлайн-очередь (DevAuth)");
            Add("mode.hint_nakama", "Casual waits for a second player, then fills bots.\nSolo is offline vs bots.", "Обычный ждёт второго игрока, затем добор ботами.\nСоло — офлайн против ботов.");
            Add("mode.back", "BACK", "НАЗАД");
            Add("mode.casual", "Casual 3v3", "Обычный 3v3");
            Add("map.ashfold", "Ashfold Lane", "Линия Ashfold");

            Add("queue.searching", "SEARCHING FOR MATCH", "ПОИСК МАТЧА");
            Add("queue.status", "Casual 3v3  ·  0:{0:00}", "Обычный 3v3  ·  0:{0:00}");
            Add("queue.filling", "Casual 3v3  ·  0:{0:00}\nFilling party with bots", "Обычный 3v3  ·  0:{0:00}\nДобор ботами");
            Add("queue.connecting", "Connecting to Nakama…", "Подключение к Nakama…");
            Add("queue.waiting", "Casual 3v3  ·  {0}s\nWaiting for another player", "Обычный 3v3  ·  {0} с\nЖдём второго игрока");
            Add("queue.joining", "Match found  ·  joining…", "Матч найден  ·  вход…");
            Add("queue.failed", "Queue failed — Cancel and retry", "Очередь не удалась — Отмена и снова");
            Add("queue.cancel", "CANCEL", "ОТМЕНА");
            Add("queue.found", "MATCH FOUND", "МАТЧ НАЙДЕН");

            Add("draft.title", "DRAFT  ·  ASHFOLD LANE", "ДРАФТ  ·  ЛИНИЯ ASHFOLD");
            Add("draft.lock", "LOCK IN", "ПОДТВЕРДИТЬ");
            Add("draft.locked", "LOCKED", "ВЫБРАН");
            Add("draft.picking", "PICKING…", "ВЫБОР…");
            Add("draft.you", "YOU · ", "ВЫ · ");
            Add("draft.bot", "BOT · ", "БОТ · ");
            Add("draft.dawn", "DAWN", "РАССВЕТ");
            Add("draft.dusk", "DUSK", "ЗАКАТ");
            Add("draft.stage", "STAGE 5.4A  ·  DRAFT", "ЭТАП 5.4A  ·  ДРАФТ");

            Add("loading.map", "ASHFOLD LANE", "ЛИНИЯ ASHFOLD");
            Add("loading.mode", "CASUAL 3v3  ·  NAKAMA QUEUE", "ОБЫЧНЫЙ 3v3  ·  ОЧЕРЕДЬ NAKAMA");
            Add("loading.entering", "Entering the fold…", "Вход в Fold…");
            Add("loading.entering_pct", "Entering the fold…  {0:P0}", "Вход в Fold…  {0:P0}");

            Add("results.victory", "VICTORY", "ПОБЕДА");
            Add("results.defeat", "DEFEAT", "ПОРАЖЕНИЕ");
            Add("results.surrender", "DEFEAT  ·  SURRENDER", "ПОРАЖЕНИЕ  ·  СДАЧА");
            Add("results.victory_surrender", "VICTORY  ·  SURRENDER", "ПОБЕДА  ·  СДАЧА");
            Add("results.continue", "CONTINUE", "ПРОДОЛЖИТЬ");
            Add("results.hdr", "PLAYER            KDA      GOLD", "ИГРОК            KDA      ЗОЛОТО");
            Add("results.line", "{0}  ·  {1}  ·  +{2} ESSENCE", "{0}  ·  {1}  ·  +{2} ЭССЕНЦИЯ");
            Add("results.stage", "STAGE 4  ·  RESULTS", "ЭТАП 4  ·  РЕЗУЛЬТАТЫ");

            Add("hud.items_empty", "ITEMS —", "ПРЕДМЕТЫ —");
            Add("hud.hint", "LMB attack  ·  minimap ping  ·  Alt+LMB  ·  Q  ·  B shop  ·  R recall", "ЛКМ атака  ·  пинг на миникарте  ·  Alt+ЛКМ  ·  Q  ·  B магазин  ·  R возврат");
            Add("hud.hint_net", "LMB move / attack  ·  minimap ping  ·  Alt+LMB  ·  10 Hz", "ЛКМ ход / атака  ·  пинг на миникарте  ·  Alt+ЛКМ  ·  10 Гц");
            Add("hud.reconnecting", "RECONNECTING  {0}s", "ПЕРЕПОДКЛЮЧЕНИЕ  {0} с");
            Add("hud.rejoin_fail", "Reconnect failed", "Не удалось переподключиться");
            Add("hud.shop", "SHOP", "МАГАЗИН");
            Add("hud.recall", "RECALL", "ВОЗВРАТ");
            Add("hud.surrender", "SURRENDER", "СДАТЬСЯ");
            Add("hud.respawn", "RESPAWN  {0}", "ВОЗРОЖДЕНИЕ  {0}");
            Add("hud.dead", "DEAD", "МЁРТВ");
            Add("hud.shop_fountain", "Shop only at fountain  ·  R to recall", "Магазин только у фонтана  ·  R для возврата");
            Add("hud.recalling", "Recalling…  {0:0.0}s", "Возврат…  {0:0.0}с");
            Add("hud.fountain", "FOUNTAIN  ·  B shop  ·  Q skill", "ФОНТАН  ·  B магазин  ·  Q умение");
            Add("hud.stage", "STAGE 6.3  ·  MAP PING", "ЭТАП 6.3  ·  ПИНГ ПО КАРТЕ");
            Add("shop.fountain", "FOUNTAIN SHOP  ·  {0} G", "МАГАЗИН ФОНТАНА  ·  {0} G");
            Add("shop.close", "CLOSE", "ЗАКРЫТЬ");
        }

        static void Add(string key, string en, string ru)
        {
            _en[key] = en;
            _ru[key] = ru;
        }
    }
}
