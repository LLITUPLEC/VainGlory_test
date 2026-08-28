using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Hall: мета Nakama. Realtime-сокет для друзей/пати; боевой матч — только после Match Found.</summary>
    public sealed class HallFlow : MonoBehaviour
    {
        Text _showcaseName;
        Image _showcaseBody;
        Text _stage;
        Text _profileLabel;
        Text _essenceLabel;
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
            RefreshShowcase(false);
            TutorialCoach.TryShowHall();
            if (Loc.ConsumeReopenAccount())
                HallAccount.Open(this, RefreshProfile);
            var social = GameSession.I != null ? GameSession.I.Social : null;
            if (social != null)
            {
                social.Matched += OnSocialMatched;
                social.QueueStarted += OnSocialQueueStarted;
                social.QueueStopped += OnSocialQueueStopped;
            }
            if (NakamaConfig.UseServer)
                StartCoroutine(ConnectMetaSocket());
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
                : new PlayerProfile { DisplayName = Loc.T("hall.guest"), Level = 1 };

            var topL = UiFactory.Box(root, new Vector2(0.02f, 0.88f), new Vector2(0.32f, 0.98f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Profile");
            _profileLabel = UiFactory.Label(topL.transform, ProfileLine(profile), 20, GameTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.Stretch(_profileLabel.rectTransform, 18, 4);

            var topR = UiFactory.Box(root, new Vector2(0.72f, 0.88f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Essence");
            _essenceLabel = UiFactory.Label(topR.transform, Loc.T("hall.essence", profile.Essence), 20, GameTheme.Gold, TextAnchor.MiddleRight);
            UiFactory.Stretch(_essenceLabel.rectTransform, 18, 4);

            var play = UiFactory.Button(root, Loc.T("hall.play"), () => _prep.OpenModeSelect(), GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(play.GetComponent<RectTransform>(), new Vector2(0.38f, 0.10f), new Vector2(0.62f, 0.20f), Vector2.zero, Vector2.zero);
            play.GetComponentInChildren<Text>().fontSize = 36;
            play.interactable = false;
            StartCoroutine(EnablePlayAfterPointerUp(play));

            BuildNav(root, 0.04f, Loc.T("hall.heroes"), () => HallCatalog.OpenHeroes(RefreshShowcase));
            BuildNav(root, 0.22f, Loc.T("hall.shop"), HallCatalog.OpenShop);
            BuildNav(root, 0.78f, Loc.T("hall.friends"), () => HallSocial.Open(this));
            BuildNav(root, 0.90f, Loc.T("hall.account"), () => HallAccount.Open(this, RefreshProfile));

            var stageBox = UiFactory.Box(root, new Vector2(0.32f, 0.02f), new Vector2(0.68f, 0.07f), Vector2.zero, Vector2.zero, Color.clear, "Stage");
            _stage = UiFactory.Label(stageBox.transform, NakamaConfig.UseServer ? Loc.T("hall.stage_nakama") : Loc.T("hall.stage_dev"), 16, GameTheme.GoldDim, TextAnchor.MiddleCenter);

            _toast = UiFactory.Box(root, new Vector2(0.25f, 0.40f), new Vector2(0.75f, 0.55f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Toast").gameObject;
            UiFactory.Label(_toast.transform, "", 22, GameTheme.Text, TextAnchor.MiddleCenter);
            _toast.SetActive(false);
        }

        IEnumerator EnablePlayAfterPointerUp(Button play)
        {
            while (BootFlow.PointerHeld())
                yield return null;
            yield return null;
            if (play != null)
                play.interactable = true;
        }

        void RefreshShowcase()
        {
            RefreshShowcase(true);
        }

        void RefreshShowcase(bool persist)
        {
            var id = GameSession.I != null ? GameSession.I.ShowcaseHeroId : "bastion";
            var hero = GameContent.GetHero(id);
            if (_showcaseBody != null)
                _showcaseBody.color = GameContent.HeroColor(hero.Id);
            if (_showcaseName != null)
                _showcaseName.text = hero.DisplayName.ToUpperInvariant() + "\n" + GameContent.RoleLabel(hero.Role);
            if (persist && GameSession.I != null)
                StartCoroutine(SaveProgressSoon());
        }

        void RefreshProfile()
        {
            var profile = GameSession.I != null ? GameSession.I.Profile : null;
            if (profile == null)
                return;
            if (_profileLabel != null)
                _profileLabel.text = ProfileLine(profile);
            if (_essenceLabel != null)
                _essenceLabel.text = Loc.T("hall.essence", profile.Essence);
        }

        static string ProfileLine(PlayerProfile profile)
        {
            var mail = profile.HasEmail ? Loc.T("hall.saved") : "";
            return Loc.T("hall.profile", profile.DisplayName.ToUpperInvariant(), profile.Level) + mail;
        }

        System.Collections.IEnumerator SaveProgressSoon()
        {
            var task = GameSession.I.SaveProgressAsync();
            while (!task.IsCompleted)
                yield return null;
        }

        static void BuildNav(Transform root, float x, string title, UnityEngine.Events.UnityAction action)
        {
            var btn = UiFactory.Button(root, title, action, GameTheme.BgPanel, GameTheme.Text);
            UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(x, 0.04f), new Vector2(x + 0.12f, 0.11f), Vector2.zero, Vector2.zero);
            btn.GetComponentInChildren<Text>().fontSize = 16;
        }

        void ShowToast(string msg)
        {
            if (_toast == null)
                return;
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

        void OnDestroy()
        {
            var social = GameSession.I != null ? GameSession.I.Social : null;
            if (social == null)
                return;
            social.Matched -= OnSocialMatched;
            social.QueueStarted -= OnSocialQueueStarted;
            social.QueueStopped -= OnSocialQueueStopped;
        }

        System.Collections.IEnumerator ConnectMetaSocket()
        {
            if (GameSession.I == null || GameSession.I.Nakama == null)
                yield break;
            var task = GameSession.I.Nakama.ConnectRealtimeAsync();
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                Debug.LogWarning("[Ashfold] Hall socket: " + task.Exception.GetBaseException().Message);
        }

        void OnSocialMatched(Nakama.IMatchmakerMatched matched)
        {
            if (_prep != null)
                _prep.JoinIncomingMatch(matched);
        }

        void OnSocialQueueStarted()
        {
            if (_prep != null)
                _prep.WaitAsPartyMember();
        }

        void OnSocialQueueStopped()
        {
            if (_prep != null)
                _prep.CancelFromPartyLeader();
        }
    }
}
