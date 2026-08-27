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
        GameObject _guestPanel;
        GameObject _emailPanel;
        InputField _nameField;
        InputField _emailField;
        InputField _passwordField;
        bool _readyToEnter;
        bool _enterRequested;
        bool _emailMode;

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

        public static bool PointerHeld()
        {
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
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

            yield return BootStep(Loc.T("boot.init_client"), 0.12f, 0.4f);
            yield return BootStep(Loc.T("boot.connecting"), 0.32f, 0.45f);

            if (!GameSession.I.IsAuthenticated)
                yield return TryRestore();

            if (GameSession.I.IsAuthenticated)
            {
                yield return BootStep(Loc.T("boot.welcome_back", GameSession.I.Profile.DisplayName), 0.7f, 0.3f);
            }
            else
            {
                yield return BootStep(Loc.T("boot.awaiting"), 0.55f, 0.25f);
                ShowLogin();
                while (!GameSession.I.IsAuthenticated)
                    yield return null;
            }

            yield return BootStep(Loc.T("boot.loading_roster"), 1f, 0.35f);
            ShowTapToEnter();

            while (!_enterRequested)
                yield return null;

            while (PointerHeld())
                yield return null;
            yield return null;

            SceneManager.LoadScene(AppScenes.Hall);
        }

        IEnumerator TryRestore()
        {
            _status.text = NakamaConfig.UseServer ? Loc.T("boot.restoring") : Loc.T("boot.signing_in");
            var task = GameSession.I.Auth.TryRestoreAsync();
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                var msg = task.Exception?.GetBaseException().Message ?? "restore failed";
                Debug.LogWarning("[Ashfold] Restore failed: " + msg);
                _status.text = Loc.T("boot.auth_failed_retry");
                yield break;
            }

            if (task.Result != null)
            {
                GameSession.I.SetProfile(task.Result);
                _status.text = Loc.T("boot.signed_in", task.Result.DisplayName)
                    + (NakamaConfig.UseServer ? "  ·  NAKAMA" : "  ·  DEV");
            }
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
                ? Loc.T("boot.signing_nakama")
                : Loc.T("boot.signing_in");
            var task = GameSession.I.Auth.SignInGuestAsync(name);
            yield return AwaitAuth(task);
        }

        IEnumerator SignInEmail(string email, string password)
        {
            _status.text = Loc.T("boot.signing_email");
            var task = GameSession.I.Auth.SignInEmailAsync(email, password);
            yield return AwaitAuth(task);
        }

        IEnumerator AwaitAuth(System.Threading.Tasks.Task<PlayerProfile> task)
        {
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                var msg = task.Exception?.GetBaseException().Message ?? "auth failed";
                Debug.LogError("[Ashfold] Sign-in failed: " + msg);
                _status.text = FriendlyAuthError(msg);
                ShowLogin(true);
                yield break;
            }

            GameSession.I.SetProfile(task.Result);
            _loginRoot.SetActive(false);
            _status.text = Loc.T("boot.signed_in", task.Result.DisplayName)
                + (NakamaConfig.UseServer ? "  ·  NAKAMA" : "  ·  DEV");
            _barFill.rectTransform.anchorMax = new Vector2(0.85f, 1f);
        }

        static string FriendlyAuthError(string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return Loc.T("boot.err.generic");
            var lower = msg.ToLowerInvariant();
            if (lower.Contains("timeout") || lower.Contains("canceled"))
                return Loc.T("boot.err.timeout");
            if (lower.Contains("invalid credentials") || lower.Contains("not found") || lower.Contains("unauthenticated"))
                return Loc.T("boot.err.credentials");
            if (lower.Contains("already in use") || lower.Contains("already exists"))
                return Loc.T("boot.err.linked");
            if (msg.Length > 80)
                return Loc.T("boot.err.generic");
            return msg;
        }

        void ShowLogin(bool keepStatus = false)
        {
            _loginRoot.SetActive(true);
            SetEmailMode(_emailMode);
            _tapHint.text = string.Empty;
            if (!keepStatus)
                _status.text = Loc.T("boot.enter_fold");
            if (string.IsNullOrEmpty(_nameField.text))
                _nameField.text = "Warrior_" + Random.Range(1000, 9999);
        }

        void ShowTapToEnter()
        {
            _loginRoot.SetActive(false);
            _readyToEnter = true;
            _tapHint.text = Loc.T("boot.tap_enter");
            _status.text = Loc.T("hall.profile", GameSession.I.Profile.DisplayName.ToUpperInvariant(), GameSession.I.Profile.Level);
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

        IEnumerator AcceptEmail()
        {
            _loginRoot.SetActive(false);
            yield return SignInEmail(_emailField.text, _passwordField.text);
        }

        void OnEmailClicked()
        {
            SetEmailMode(true);
            _status.text = Loc.T("boot.email_hint");
        }

        void OnEmailBack()
        {
            SetEmailMode(false);
            _status.text = Loc.T("boot.enter_fold");
        }

        void OnRetryClicked()
        {
            if (!_loginRoot.activeSelf)
                return;
            if (_emailMode)
                StartCoroutine(AcceptEmail());
            else
                StartCoroutine(AcceptGuest());
        }

        void SetEmailMode(bool email)
        {
            _emailMode = email && GameSession.I.Auth.SupportsEmail;
            if (_guestPanel != null)
                _guestPanel.SetActive(!_emailMode);
            if (_emailPanel != null)
                _emailPanel.SetActive(_emailMode);
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
            UiFactory.Label(sub.transform, Loc.T("boot.subtitle"), 22, GameTheme.Teal, TextAnchor.MiddleCenter);

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
            _status = UiFactory.Label(statusBox.transform, Loc.T("boot.init"), 20, GameTheme.TextMuted, TextAnchor.MiddleCenter);

            var tapBox = UiFactory.Box(root, new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.14f), Vector2.zero, Vector2.zero, Color.clear, "Tap");
            _tapHint = UiFactory.Label(tapBox.transform, string.Empty, 26, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            var stage = UiFactory.Box(root, new Vector2(0.02f, 0.94f), new Vector2(0.4f, 0.99f), Vector2.zero, Vector2.zero, Color.clear, "Stage");
            UiFactory.Label(stage.transform, NakamaConfig.UseServer ? Loc.T("boot.stage_nakama") : Loc.T("boot.stage_dev"), 16, GameTheme.GoldDim, TextAnchor.MiddleLeft);

            var ver = UiFactory.Box(root, new Vector2(0.6f, 0.01f), new Vector2(0.98f, 0.05f), Vector2.zero, Vector2.zero, Color.clear, "Ver");
            UiFactory.Label(ver.transform, Loc.T("boot.footer"), 14, GameTheme.TextMuted, TextAnchor.MiddleRight);

            BuildLogin(root);
        }

        void BuildLogin(Transform root)
        {
            _loginRoot = UiFactory.Box(root, new Vector2(0.28f, 0.12f), new Vector2(0.72f, 0.40f), Vector2.zero, Vector2.zero, Color.clear, "Login").gameObject;
            _loginRoot.SetActive(false);

            _guestPanel = UiFactory.Box(_loginRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear, "Guest").gameObject;
            _nameField = UiFactory.Input(_guestPanel.transform, Loc.T("boot.name_ph"));
            UiFactory.SetAnchors(_nameField.GetComponent<RectTransform>(), new Vector2(0f, 0.58f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var guest = UiFactory.Button(_guestPanel.transform, Loc.T("boot.play_guest"), OnGuestClicked, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(guest.GetComponent<RectTransform>(), new Vector2(0f, 0.22f), new Vector2(0.48f, 0.52f), Vector2.zero, Vector2.zero);
            guest.GetComponentInChildren<Text>().fontSize = 20;

            var emailBtn = UiFactory.Button(_guestPanel.transform, Loc.T("boot.email"), OnEmailClicked, GameTheme.BgPanel, GameTheme.Text);
            UiFactory.SetAnchors(emailBtn.GetComponent<RectTransform>(), new Vector2(0.52f, 0.22f), new Vector2(1f, 0.52f), Vector2.zero, Vector2.zero);
            emailBtn.GetComponentInChildren<Text>().fontSize = 20;
            emailBtn.interactable = NakamaConfig.UseServer;

            var retry = UiFactory.Button(_guestPanel.transform, Loc.T("boot.retry"), OnRetryClicked, GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(retry.GetComponent<RectTransform>(), new Vector2(0.2f, 0f), new Vector2(0.8f, 0.18f), Vector2.zero, Vector2.zero);
            retry.GetComponentInChildren<Text>().fontSize = 16;

            _emailPanel = UiFactory.Box(_loginRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear, "Email").gameObject;
            _emailField = UiFactory.Input(_emailPanel.transform, Loc.T("boot.email_ph"), 80, InputField.ContentType.EmailAddress);
            UiFactory.SetAnchors(_emailField.GetComponent<RectTransform>(), new Vector2(0f, 0.70f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _emailField.GetComponentInChildren<Text>().fontSize = 22;
            _passwordField = UiFactory.Input(_emailPanel.transform, Loc.T("boot.pass_ph"), 64, InputField.ContentType.Password);
            UiFactory.SetAnchors(_passwordField.GetComponent<RectTransform>(), new Vector2(0f, 0.38f), new Vector2(1f, 0.66f), Vector2.zero, Vector2.zero);
            _passwordField.GetComponentInChildren<Text>().fontSize = 22;

            var signIn = UiFactory.Button(_emailPanel.transform, Loc.T("boot.sign_in"), () => StartCoroutine(AcceptEmail()), GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(signIn.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.48f, 0.32f), Vector2.zero, Vector2.zero);
            signIn.GetComponentInChildren<Text>().fontSize = 20;

            var back = UiFactory.Button(_emailPanel.transform, Loc.T("boot.back"), OnEmailBack, GameTheme.BgPanel, GameTheme.Text);
            UiFactory.SetAnchors(back.GetComponent<RectTransform>(), new Vector2(0.52f, 0f), new Vector2(1f, 0.32f), Vector2.zero, Vector2.zero);
            back.GetComponentInChildren<Text>().fontSize = 20;

            _emailPanel.SetActive(false);
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
