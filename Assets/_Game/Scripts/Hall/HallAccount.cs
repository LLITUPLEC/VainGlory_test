using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Привязка email к device-аккаунту (восстановление на другом устройстве).</summary>
    public static class HallAccount
    {
        public static void Open(MonoBehaviour host, System.Action onChanged)
        {
            var canvas = AppUi.OverlayCanvas("AccountOverlay");
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.78f), "Dim");
            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.16f, 0.30f), new Vector2(0.84f, 0.92f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");

            var titleBox = UiFactory.Box(sheet.transform, new Vector2(0.04f, 0.88f), new Vector2(0.62f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "Title");
            UiFactory.Label(titleBox.transform, Loc.T("account.title"), 28, GameTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);

            var globeBox = UiFactory.Box(sheet.transform, new Vector2(0.66f, 0.88f), new Vector2(0.78f, 0.98f), new Vector2(6, 6), new Vector2(-6, -6), GameTheme.BgPanelSoft, "Lang");
            var globe = UiFactory.GlobeButton(globeBox.transform, OpenLanguagePicker);
            UiFactory.Stretch(globe.GetComponent<RectTransform>(), 4, 4);

            var close = UiFactory.Button(sheet.transform, Loc.T("account.close"), () => Object.Destroy(canvas.gameObject), GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(close.GetComponent<RectTransform>(), new Vector2(0.80f, 0.88f), new Vector2(0.98f, 0.98f), new Vector2(8, 8), new Vector2(-8, -8));
            close.GetComponentInChildren<Text>().fontSize = 16;

            var profile = GameSession.I != null ? GameSession.I.Profile : null;
            var name = profile != null ? profile.DisplayName : Loc.T("hall.guest");
            var email = profile != null && profile.HasEmail ? profile.Email : Loc.T("account.email_none");
            var info = UiFactory.Box(sheet.transform, new Vector2(0.06f, 0.70f), new Vector2(0.94f, 0.86f), Vector2.zero, Vector2.zero, Color.clear, "Info");
            UiFactory.Label(info.transform,
                Loc.T("account.info", name.ToUpperInvariant(), profile != null ? profile.Level : 1, email),
                18, GameTheme.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, true);

            var statusBox = UiFactory.Box(sheet.transform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.16f), Vector2.zero, Vector2.zero, Color.clear, "St");
            var status = UiFactory.Label(statusBox.transform, "", 16, GameTheme.Teal, TextAnchor.MiddleCenter);

            if (profile != null && profile.HasEmail)
            {
                UiFactory.Label(
                    UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.66f), Vector2.zero, Vector2.zero, Color.clear, "Ok").transform,
                    Loc.T("account.tied"),
                    18, GameTheme.Text, TextAnchor.MiddleCenter, FontStyle.Normal, true);
            }
            else if (GameSession.I != null && GameSession.I.Auth.SupportsEmail)
            {
                var emailField = UiFactory.Input(sheet.transform, Loc.T("boot.email_ph"), 80, InputField.ContentType.EmailAddress);
                UiFactory.SetAnchors(emailField.GetComponent<RectTransform>(), new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.66f), Vector2.zero, Vector2.zero);
                emailField.GetComponentInChildren<Text>().fontSize = 20;

                var passField = UiFactory.Input(sheet.transform, Loc.T("boot.pass_ph"), 64, InputField.ContentType.Password);
                UiFactory.SetAnchors(passField.GetComponent<RectTransform>(), new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.52f), Vector2.zero, Vector2.zero);
                passField.GetComponentInChildren<Text>().fontSize = 20;

                var link = UiFactory.Button(sheet.transform, Loc.T("account.link"), () =>
                {
                    host.StartCoroutine(LinkRoutine(emailField.text, passField.text, status, onChanged, canvas.gameObject));
                }, GameTheme.Gold, GameTheme.Bg);
                UiFactory.SetAnchors(link.GetComponent<RectTransform>(), new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.36f), Vector2.zero, Vector2.zero);
                link.GetComponentInChildren<Text>().fontSize = 20;

                KeyboardLift.Attach(sheet.rectTransform);
            }
            else
            {
                UiFactory.Label(
                    UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.66f), Vector2.zero, Vector2.zero, Color.clear, "Dev").transform,
                    Loc.T("account.need_nakama"),
                    18, GameTheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, true);
            }

            if (profile != null && profile.HasEmail)
            {
                var signOut = UiFactory.Button(sheet.transform, Loc.T("account.sign_out"), () =>
                {
                    GameSession.I.SignOut();
                    Object.Destroy(canvas.gameObject);
                    SceneManager.LoadScene(AppScenes.Boot);
                }, GameTheme.Crimson, GameTheme.Text);
                UiFactory.SetAnchors(signOut.GetComponent<RectTransform>(), new Vector2(0.25f, 0.17f), new Vector2(0.75f, 0.23f), Vector2.zero, Vector2.zero);
                signOut.GetComponentInChildren<Text>().fontSize = 16;
            }
        }

        static void OpenLanguagePicker()
        {
            var canvas = AppUi.OverlayCanvas("LangOverlay", 40);
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.55f), "Dim");
            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.34f, 0.28f), new Vector2(0.66f, 0.72f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");

            UiFactory.Label(
                UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero, Color.clear, "T").transform,
                Loc.T("lang.title"), 26, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            UiFactory.Label(
                UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero, Color.clear, "H").transform,
                Loc.T("lang.hint"), 16, GameTheme.TextMuted, TextAnchor.MiddleCenter);

            for (var i = 0; i < Loc.Languages.Length; i++)
            {
                var lang = Loc.Languages[i];
                var code = lang.Code;
                var selected = code == Loc.Code;
                var y = 0.40f - i * 0.20f;
                var btn = UiFactory.Button(sheet.transform, lang.NativeName, () =>
                {
                    Object.Destroy(canvas.gameObject);
                    Loc.Set(code, reopenAccount: true);
                }, selected ? GameTheme.Gold : GameTheme.BgPanelSoft, selected ? GameTheme.Bg : GameTheme.Text);
                UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(0.10f, y), new Vector2(0.90f, y + 0.16f), Vector2.zero, Vector2.zero);
                btn.GetComponentInChildren<Text>().fontSize = 22;
            }

            var close = UiFactory.Button(sheet.transform, Loc.T("account.close"), () => Object.Destroy(canvas.gameObject), GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(close.GetComponent<RectTransform>(), new Vector2(0.22f, 0.06f), new Vector2(0.78f, 0.18f), Vector2.zero, Vector2.zero);
            close.GetComponentInChildren<Text>().fontSize = 16;
        }

        static IEnumerator LinkRoutine(string email, string password, Text status, System.Action onChanged, GameObject canvas)
        {
            status.text = Loc.T("account.linking");
            var task = GameSession.I.Auth.LinkEmailAsync(email, password);
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
            {
                var msg = task.Exception?.GetBaseException().Message ?? "link failed";
                Debug.LogError("[Ashfold] Link email failed: " + msg);
                status.color = GameTheme.Crimson;
                status.text = msg.Length > 90 ? Loc.T("account.link_failed") : msg;
                yield break;
            }

            GameSession.I.SetProfile(task.Result);
            status.color = GameTheme.Teal;
            status.text = Loc.T("account.linked", task.Result.Email);
            onChanged?.Invoke();
            yield return new WaitForSeconds(0.8f);
            if (canvas != null)
                Object.Destroy(canvas);
        }
    }
}
