using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Стартовый поток VG: сплэш → шаги загрузки → гость → Hall.</summary>
    public sealed class BootFlow : MonoBehaviour
    {
        [SerializeField] bool skipAuthForDev;

        Text _status;
        Text _tapHint;
        Image _barFill;
        GameObject _loginRoot;
        InputField _nameField;
        bool _readyToEnter;
        bool _enterRequested;

        void Awake()
        {
            EnsureEventSystem();
            if (Camera.main != null)
                Camera.main.backgroundColor = GameTheme.Bg;
        }

        void Start()
        {
            BuildUi();
            StartCoroutine(Run());
        }

        void Update()
        {
            if (!_readyToEnter || _loginRoot.activeSelf)
                return;
            if (Pressed())
                _enterRequested = true;
        }

        static bool Pressed()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;
            return false;
        }

        IEnumerator Run()
        {
            if (skipAuthForDev && GameSession.I.IsAuthenticated)
            {
                SceneManager.LoadScene(AppScenes.Hall);
                yield break;
            }

            yield return BootStep("Initializing client…", 0.12f, 0.4f);
            yield return BootStep("Connecting…", 0.32f, 0.45f);

            if (GameSession.I.IsAuthenticated)
            {
                yield return BootStep("Welcome back, " + GameSession.I.Profile.DisplayName, 0.7f, 0.3f);
                yield return BootStep("Loading roster · 3 heroes, 6 items", 1f, 0.35f);
                ShowTapToEnter();
            }
            else if (skipAuthForDev || DevAuthService.HasSavedGuest)
            {
                yield return SignIn(PlayerPrefs.GetString(DevAuthService.KeyName, string.Empty));
                if (!GameSession.I.IsAuthenticated)
                {
                    ShowLogin();
                    while (!GameSession.I.IsAuthenticated)
                        yield return null;
                }
                yield return BootStep("Loading roster · 3 heroes, 6 items", 1f, 0.35f);
                ShowTapToEnter();
            }
            else
            {
                yield return BootStep("Awaiting commander…", 0.55f, 0.25f);
                ShowLogin();
                while (!GameSession.I.IsAuthenticated)
                    yield return null;
                yield return BootStep("Loading roster · 3 heroes, 6 items", 1f, 0.4f);
                ShowTapToEnter();
            }

            while (!_enterRequested)
                yield return null;

            SceneManager.LoadScene(AppScenes.Hall);
        }

        IEnumerator BootStep(string text, float progress, float seconds)
        {
            _status.text = text;
            var start = _barFill.rectTransform.anchorMax.x;
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                var x = Mathf.Lerp(start, progress, t / seconds);
                _barFill.rectTransform.anchorMax = new Vector2(x, 1f);
                yield return null;
            }
        }

        IEnumerator SignIn(string name)
        {
            _status.text = NakamaConfig.UseServer
                ? "Signing in via Nakama…"
                : "Signing in…";
            var task = GameSession.I.Auth.SignInGuestAsync(name);
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                var msg = task.Exception?.GetBaseException().Message ?? "auth failed";
                Debug.LogError("[Ashfold] Sign-in failed: " + msg);
                _status.text = "Auth failed — check VPS / Host";
                _loginRoot.SetActive(true);
                yield break;
            }

            GameSession.I.SetProfile(task.Result);
            _status.text = "Signed in as " + task.Result.DisplayName
                + (NakamaConfig.UseServer ? "  ·  NAKAMA" : "  ·  DEV");
            _barFill.rectTransform.anchorMax = new Vector2(0.85f, 1f);
        }

        void ShowLogin()
        {
            _loginRoot.SetActive(true);
            _tapHint.text = string.Empty;
            _status.text = "ENTER THE FOLD";
            if (string.IsNullOrEmpty(_nameField.text))
                _nameField.text = "Warrior_" + Random.Range(1000, 9999);
        }

        void ShowTapToEnter()
        {
            _loginRoot.SetActive(false);
            _readyToEnter = true;
            _tapHint.text = "TAP TO ENTER";
            _status.text = GameSession.I.Profile.DisplayName.ToUpperInvariant() + "  ·  LVL " + GameSession.I.Profile.Level;
            _barFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        }

        void OnGuestClicked()
        {
            if (_loginRoot == null || !_loginRoot.activeSelf)
                return;
            StartCoroutine(AcceptGuest());
        }

        IEnumerator AcceptGuest()
        {
            _loginRoot.SetActive(false);
            yield return SignIn(_nameField.text);
        }

        void BuildUi()
        {
            var canvas = UiFactory.CreateCanvas("BootCanvas");
            var root = canvas.transform;

            UiFactory.Panel(root, GameTheme.Bg, "Bg");

            UiFactory.Box(root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, -1), new Vector2(0, 1), GameTheme.Line, "HLine");
            UiFactory.Box(root, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.88f), new Vector2(-1, 0), new Vector2(1, 0), GameTheme.Hex(0x3DCEC7, 0.12f), "VLine");

            var mark = UiFactory.Box(root, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(-70, -70), new Vector2(70, 70), GameTheme.Hex(0xD4B45A, 0.08f), "Mark");
            UiFactory.Label(mark.transform, "A", 72, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            var title = UiFactory.Box(root, new Vector2(0.1f, 0.48f), new Vector2(0.9f, 0.62f), Vector2.zero, Vector2.zero, Color.clear, "TitleBox");
            UiFactory.Label(title.transform, "ASHFOLD", 96, GameTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);

            var sub = UiFactory.Box(root, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.50f), Vector2.zero, Vector2.zero, Color.clear, "SubBox");
            UiFactory.Label(sub.transform, "BATTLE FOR THE FOLD  ·  3v3", 22, GameTheme.Teal, TextAnchor.MiddleCenter);

            var barBg = UiFactory.Box(root, new Vector2(0.32f, 0.22f), new Vector2(0.68f, 0.235f), Vector2.zero, Vector2.zero, GameTheme.Hex(0xFFFFFF, 0.08f), "BarBg");
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            _barFill = fillGo.GetComponent<Image>();
            _barFill.color = GameTheme.Gold;
            _barFill.raycastTarget = false;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.02f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var statusBox = UiFactory.Box(root, new Vector2(0.15f, 0.16f), new Vector2(0.85f, 0.21f), Vector2.zero, Vector2.zero, Color.clear, "Status");
            _status = UiFactory.Label(statusBox.transform, "Initializing…", 20, GameTheme.TextMuted, TextAnchor.MiddleCenter);

            var tapBox = UiFactory.Box(root, new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.14f), Vector2.zero, Vector2.zero, Color.clear, "Tap");
            _tapHint = UiFactory.Label(tapBox.transform, string.Empty, 26, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            var stage = UiFactory.Box(root, new Vector2(0.02f, 0.94f), new Vector2(0.4f, 0.99f), Vector2.zero, Vector2.zero, Color.clear, "Stage");
            UiFactory.Label(stage.transform, NakamaConfig.UseServer ? "STAGE 1.x  ·  NAKAMA" : "STAGE 0.2  ·  BOOT", 16, GameTheme.GoldDim, TextAnchor.MiddleLeft);

            var ver = UiFactory.Box(root, new Vector2(0.6f, 0.01f), new Vector2(0.98f, 0.05f), Vector2.zero, Vector2.zero, Color.clear, "Ver");
            UiFactory.Label(ver.transform, "NAKAMA SELF-HOST  ·  GO MATCH  ·  PROTOTYPE", 14, GameTheme.TextMuted, TextAnchor.MiddleRight);

            BuildLogin(root);
        }

        void BuildLogin(Transform root)
        {
            _loginRoot = UiFactory.Box(root, new Vector2(0.32f, 0.26f), new Vector2(0.68f, 0.40f), Vector2.zero, Vector2.zero, Color.clear, "Login").gameObject;
            _loginRoot.SetActive(false);

            _nameField = UiFactory.Input(_loginRoot.transform, "Commander name");
            UiFactory.SetAnchors(_nameField.GetComponent<RectTransform>(), new Vector2(0f, 0.52f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var btn = UiFactory.Button(_loginRoot.transform, "PLAY AS GUEST", OnGuestClicked, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
