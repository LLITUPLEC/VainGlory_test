using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Hall VG-компоновка + каталог + PLAY в очередь.</summary>
    public sealed class HallFlow : MonoBehaviour
    {
        Text _showcaseName;
        Image _showcaseBody;
        Text _stage;
        MatchPrepFlow _prep;
        GameObject _toast;

        void Awake()
        {
            AppUi.EnsureEventSystem();
            if (Camera.main != null)
                Camera.main.backgroundColor = GameTheme.Bg;
            _prep = gameObject.GetComponent<MatchPrepFlow>() ?? gameObject.AddComponent<MatchPrepFlow>();
        }

        void Start()
        {
            Build();
            RefreshShowcase();
        }

        void Build()
        {
            var canvas = UiFactory.CreateCanvas("HallCanvas");
            var root = canvas.transform;
            UiFactory.Panel(root, GameTheme.Bg, "Bg");

            UiFactory.Box(root, new Vector2(0.18f, 0.22f), new Vector2(0.82f, 0.86f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x3DCEC7, 0.05f), "Showcase");
            _showcaseBody = UiFactory.Box(root, new Vector2(0.38f, 0.32f), new Vector2(0.62f, 0.80f), Vector2.zero, Vector2.zero, GameTheme.Gold, "HeroStand");
            _showcaseName = UiFactory.Label(_showcaseBody.transform, "BASTION", 28, GameTheme.Bg, TextAnchor.LowerCenter, FontStyle.Bold);

            var profile = GameSession.I != null && GameSession.I.IsAuthenticated
                ? GameSession.I.Profile
                : new PlayerProfile { DisplayName = "Guest", Level = 1 };

            var topL = UiFactory.Box(root, new Vector2(0.02f, 0.88f), new Vector2(0.32f, 0.98f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Profile");
            var pLabel = UiFactory.Label(topL.transform, profile.DisplayName.ToUpperInvariant() + "   LVL " + profile.Level, 20, GameTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.Stretch(pLabel.rectTransform, 18, 4);

            var topR = UiFactory.Box(root, new Vector2(0.72f, 0.88f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Essence");
            var eLabel = UiFactory.Label(topR.transform, "ESSENCE  " + profile.Essence, 20, GameTheme.Gold, TextAnchor.MiddleRight);
            UiFactory.Stretch(eLabel.rectTransform, 18, 4);

            var play = UiFactory.Button(root, "PLAY", () => _prep.OpenModeSelect(), GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(play.GetComponent<RectTransform>(), new Vector2(0.38f, 0.10f), new Vector2(0.62f, 0.20f), Vector2.zero, Vector2.zero);
            play.GetComponentInChildren<Text>().fontSize = 36;

            BuildNav(root, 0.04f, "HEROES", () => HallCatalog.OpenHeroes(RefreshShowcase));
            BuildNav(root, 0.22f, "SHOP", HallCatalog.OpenShop);
            BuildNav(root, 0.78f, "FRIENDS", () => ShowToast("Friends — этап 7 (Nakama)"));
            BuildNav(root, 0.90f, "QUESTS", () => ShowToast("Quests — позже"));

            var stageBox = UiFactory.Box(root, new Vector2(0.32f, 0.02f), new Vector2(0.68f, 0.07f), Vector2.zero, Vector2.zero, Color.clear, "Stage");
            _stage = UiFactory.Label(stageBox.transform, "STAGE 2.1  ·  HALL", 16, GameTheme.GoldDim, TextAnchor.MiddleCenter);

            _toast = UiFactory.Box(root, new Vector2(0.25f, 0.40f), new Vector2(0.75f, 0.55f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Toast").gameObject;
            UiFactory.Label(_toast.transform, "", 22, GameTheme.Text, TextAnchor.MiddleCenter);
            _toast.SetActive(false);
        }

        void RefreshShowcase()
        {
            var id = GameSession.I != null ? GameSession.I.ShowcaseHeroId : "bastion";
            var hero = GameContent.GetHero(id);
            if (_showcaseBody != null)
                _showcaseBody.color = GameContent.HeroColor(hero.Id);
            if (_showcaseName != null)
                _showcaseName.text = hero.DisplayName.ToUpperInvariant() + "\n" + GameContent.RoleLabel(hero.Role);
        }

        static void BuildNav(Transform root, float x, string title, UnityEngine.Events.UnityAction action)
        {
            var btn = UiFactory.Button(root, title, action, GameTheme.BgPanel, GameTheme.Text);
            UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(x, 0.04f), new Vector2(x + 0.12f, 0.11f), Vector2.zero, Vector2.zero);
            btn.GetComponentInChildren<Text>().fontSize = 16;
        }

        void ShowToast(string msg)
        {
            _toast.SetActive(true);
            _toast.GetComponentInChildren<Text>().text = msg;
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.4f);
        }

        void HideToast()
        {
            if (_toast != null)
                _toast.SetActive(false);
        }
    }
}
