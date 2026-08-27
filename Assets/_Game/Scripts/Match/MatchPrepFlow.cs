using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>PLAY → режим → очередь → драфт → loading. Photon не трогаем (этап 5).</summary>
    public sealed class MatchPrepFlow : MonoBehaviour
    {
        const float QueueSeconds = 3.2f;
        const float FoundSeconds = 1.6f;
        const float DraftSeconds = 20f;
        const float LoadingSeconds = 4f;

        GameObject _overlay;
        Text _status;
        Text _timer;
        Text[] _slotLabels;
        Image[] _heroCards;
        Button _lockBtn;
        string _pickedId;
        Coroutine _routine;
        bool _locked;

        public void OpenModeSelect()
        {
            CloseOverlay();
            var canvas = AppUi.OverlayCanvas("ModeOverlay");
            _overlay = canvas.gameObject;
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.78f), "Dim");

            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.28f, 0.22f), new Vector2(0.72f, 0.78f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            UiFactory.Label(sheet.transform, "SELECT MODE", 28, GameTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);

            var casual = UiFactory.Button(sheet.transform, "CASUAL  3v3", StartQueue, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(casual.GetComponent<RectTransform>(), new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.68f), Vector2.zero, Vector2.zero);
            casual.GetComponentInChildren<Text>().fontSize = 32;

            var hint = UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.40f), Vector2.zero, Vector2.zero, Color.clear, "Hint");
            UiFactory.Label(hint.transform, "Lane + jungle  ·  one turret  ·  crystal\nQueue is local until Nakama (5.8)", 18, GameTheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, true);

            var cancel = UiFactory.Button(sheet.transform, "BACK", CloseOverlay, GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.18f), Vector2.zero, Vector2.zero);
            cancel.GetComponentInChildren<Text>().fontSize = 20;
        }

        void StartQueue()
        {
            CloseOverlay();
            BuildQueue();
            _routine = StartCoroutine(QueueRoutine());
        }

        void BuildQueue()
        {
            var canvas = AppUi.OverlayCanvas("QueueOverlay");
            _overlay = canvas.gameObject;
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.82f), "Dim");

            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.22f, 0.28f), new Vector2(0.78f, 0.72f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            UiFactory.Label(sheet.transform, "SEARCHING FOR MATCH", 30, GameTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);

            var statusBox = UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.62f), Vector2.zero, Vector2.zero, Color.clear, "Status");
            _status = UiFactory.Label(statusBox.transform, "Casual 3v3  ·  0:00", 24, GameTheme.Teal, TextAnchor.MiddleCenter);

            var cancel = UiFactory.Button(sheet.transform, "CANCEL", CancelQueue, GameTheme.Crimson, GameTheme.Text);
            UiFactory.SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.25f, 0.10f), new Vector2(0.75f, 0.28f), Vector2.zero, Vector2.zero);
        }

        IEnumerator QueueRoutine()
        {
            var t = 0f;
            while (t < QueueSeconds)
            {
                t += Time.deltaTime;
                var sec = Mathf.FloorToInt(t);
                if (_status != null)
                    _status.text = "Casual 3v3  ·  0:" + sec.ToString("00") + "\nFilling party with bots";
                yield return null;
            }

            GameSession.I.Match = CreateMatch();
            ShowMatchFound();
            yield return new WaitForSeconds(FoundSeconds);
            OpenDraft();
        }

        void ShowMatchFound()
        {
            if (_status != null)
                _status.text = "MATCH FOUND";
        }

        void CancelQueue()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            GameSession.I.Match = null;
            CloseOverlay();
        }

        static MatchSession CreateMatch()
        {
            var match = new MatchSession();
            var me = GameSession.I.Profile != null ? GameSession.I.Profile.DisplayName : "Player";
            match.Players.Add(new MatchParticipant { Name = me, IsLocal = true, Team = 0, Slot = 0 });

            var bot = 0;
            for (var slot = 1; slot < 3; slot++)
                match.Players.Add(new MatchParticipant { Name = GameContent.BotNames[bot++], IsBot = true, Team = 0, Slot = slot });
            for (var slot = 0; slot < 3; slot++)
                match.Players.Add(new MatchParticipant { Name = GameContent.BotNames[bot++], IsBot = true, Team = 1, Slot = slot });
            return match;
        }

        void OpenDraft()
        {
            CloseOverlay();
            _pickedId = GameSession.I.ShowcaseHeroId;
            _locked = false;
            var canvas = AppUi.OverlayCanvas("DraftOverlay");
            _overlay = canvas.gameObject;
            UiFactory.Panel(canvas.transform, GameTheme.Bg, "Bg");

            UiFactory.Label(
                UiFactory.Box(canvas.transform, new Vector2(0.2f, 0.90f), new Vector2(0.8f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "Title").transform,
                "DRAFT  ·  ASHFOLD LANE", 26, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            _timer = UiFactory.Label(
                UiFactory.Box(canvas.transform, new Vector2(0.42f, 0.82f), new Vector2(0.58f, 0.90f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Timer").transform,
                "0:20", 28, GameTheme.Teal, TextAnchor.MiddleCenter, FontStyle.Bold);

            _slotLabels = new Text[6];
            BuildTeamColumn(canvas.transform, 0, 0.02f, "DAWN");
            BuildTeamColumn(canvas.transform, 1, 0.70f, "DUSK");

            _heroCards = new Image[GameContent.Heroes.Length];
            for (var i = 0; i < GameContent.Heroes.Length; i++)
            {
                var hero = GameContent.Heroes[i];
                var x0 = 0.28f + i * 0.15f;
                var btn = UiFactory.Button(canvas.transform, hero.DisplayName.ToUpperInvariant(), () => SelectHero(hero.Id), GameTheme.BgPanel, GameTheme.Text);
                UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(x0, 0.30f), new Vector2(x0 + 0.14f, 0.72f), Vector2.zero, Vector2.zero);
                btn.GetComponentInChildren<Text>().fontSize = 18;
                _heroCards[i] = btn.GetComponent<Image>();
                var swatch = UiFactory.Box(btn.transform, new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.92f), Vector2.zero, Vector2.zero, GameContent.HeroColor(hero.Id), "C");
                swatch.raycastTarget = false;
            }

            _lockBtn = UiFactory.Button(canvas.transform, "LOCK IN", LockIn, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(_lockBtn.GetComponent<RectTransform>(), new Vector2(0.38f, 0.08f), new Vector2(0.62f, 0.18f), Vector2.zero, Vector2.zero);

            var stage = UiFactory.Box(canvas.transform, new Vector2(0.02f, 0.01f), new Vector2(0.3f, 0.06f), Vector2.zero, Vector2.zero, Color.clear, "St");
            UiFactory.Label(stage.transform, "STAGE 2.7  ·  DRAFT", 14, GameTheme.GoldDim, TextAnchor.MiddleLeft);

            SelectHero(_pickedId);
            RefreshSlots();
            _routine = StartCoroutine(DraftRoutine());
        }

        void BuildTeamColumn(Transform root, int team, float x, string title)
        {
            var col = UiFactory.Box(root, new Vector2(x, 0.18f), new Vector2(x + 0.28f, 0.82f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Team" + team);
            var head = UiFactory.Box(col.transform, new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "Head");
            UiFactory.Label(head.transform, title, 20, team == 0 ? GameTheme.Teal : GameTheme.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold);

            var i = 0;
            foreach (var p in GameSession.I.Match.Team(team))
            {
                var y = 0.62f - i * 0.22f;
                var slot = UiFactory.Box(col.transform, new Vector2(0.08f, y), new Vector2(0.92f, y + 0.18f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "S");
                var idx = team * 3 + p.Slot;
                _slotLabels[idx] = UiFactory.Label(slot.transform, p.Name, 16, GameTheme.Text, TextAnchor.MiddleLeft, FontStyle.Normal, true);
                UiFactory.Stretch(_slotLabels[idx].rectTransform, 10, 4);
                i++;
            }
        }

        void SelectHero(string id)
        {
            if (_locked)
                return;
            _pickedId = id;
            for (var i = 0; i < GameContent.Heroes.Length; i++)
            {
                var selected = GameContent.Heroes[i].Id == id;
                _heroCards[i].color = selected ? GameTheme.GoldDim : GameTheme.BgPanel;
            }
        }

        void LockIn()
        {
            if (_locked)
                return;
            _locked = true;
            var local = GameSession.I.Match.Local;
            local.HeroId = _pickedId;
            local.Locked = true;
            _lockBtn.interactable = false;
            _lockBtn.GetComponentInChildren<Text>().text = "LOCKED";
            RefreshSlots();
        }

        IEnumerator DraftRoutine()
        {
            var t = DraftSeconds;
            var botPickAt = new[] { 4f, 7f, 10f, 13f, 16f };
            var botsQueued = 0;

            while (t > 0f)
            {
                t -= Time.deltaTime;
                if (_timer != null)
                    _timer.text = "0:" + Mathf.CeilToInt(t).ToString("00");

                var elapsed = DraftSeconds - t;
                while (botsQueued < botPickAt.Length && elapsed >= botPickAt[botsQueued])
                {
                    LockNextBot(botsQueued);
                    botsQueued++;
                    RefreshSlots();
                }

                if (_locked && AllLocked())
                    break;
                yield return null;
            }

            if (!_locked)
                LockIn();
            while (!AllLocked())
            {
                LockNextBot(botsQueued++);
                RefreshSlots();
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);
            OpenLoading();
        }

        void LockNextBot(int index)
        {
            var bots = new System.Collections.Generic.List<MatchParticipant>();
            foreach (var p in GameSession.I.Match.Players)
            {
                if (p.IsBot && !p.Locked)
                    bots.Add(p);
            }
            if (bots.Count == 0)
                return;
            var bot = bots[0];
            bot.HeroId = GameContent.Heroes[index % GameContent.Heroes.Length].Id;
            bot.Locked = true;
        }

        bool AllLocked()
        {
            foreach (var p in GameSession.I.Match.Players)
            {
                if (!p.Locked)
                    return false;
            }
            return true;
        }

        void RefreshSlots()
        {
            if (_slotLabels == null || GameSession.I.Match == null)
                return;
            foreach (var p in GameSession.I.Match.Players)
            {
                var idx = p.Team * 3 + p.Slot;
                if (idx < 0 || idx >= _slotLabels.Length || _slotLabels[idx] == null)
                    continue;
                var hero = string.IsNullOrEmpty(p.HeroId) ? "PICKING…" : GameContent.GetHero(p.HeroId).DisplayName.ToUpperInvariant();
                var tag = p.IsLocal ? "YOU · " : p.IsBot ? "BOT · " : "";
                _slotLabels[idx].text = tag + p.Name + "\n" + hero;
            }
        }

        void OpenLoading()
        {
            CloseOverlay();
            var canvas = AppUi.OverlayCanvas("LoadingOverlay");
            _overlay = canvas.gameObject;
            UiFactory.Panel(canvas.transform, GameTheme.Bg, "Bg");

            UiFactory.Label(
                UiFactory.Box(canvas.transform, new Vector2(0.15f, 0.78f), new Vector2(0.85f, 0.90f), Vector2.zero, Vector2.zero, Color.clear, "T").transform,
                "ASHFOLD LANE", 36, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            UiFactory.Label(
                UiFactory.Box(canvas.transform, new Vector2(0.15f, 0.70f), new Vector2(0.85f, 0.78f), Vector2.zero, Vector2.zero, Color.clear, "M").transform,
                "CASUAL 3v3  ·  PHOTON OFF (STAGE 5)", 16, GameTheme.TextMuted, TextAnchor.MiddleCenter);

            DrawLoadingTeam(canvas.transform, 0, 0.06f, "DAWN");
            DrawLoadingTeam(canvas.transform, 1, 0.52f, "DUSK");

            _status = UiFactory.Label(
                UiFactory.Box(canvas.transform, new Vector2(0.2f, 0.08f), new Vector2(0.8f, 0.16f), Vector2.zero, Vector2.zero, Color.clear, "L").transform,
                "Entering the fold…", 20, GameTheme.Teal, TextAnchor.MiddleCenter);

            _routine = StartCoroutine(LoadingRoutine());
        }

        void DrawLoadingTeam(Transform root, int team, float x, string title)
        {
            var col = UiFactory.Box(root, new Vector2(x, 0.22f), new Vector2(x + 0.42f, 0.68f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "LT");
            var head = UiFactory.Box(col.transform, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "H");
            UiFactory.Label(head.transform, title, 22, team == 0 ? GameTheme.Teal : GameTheme.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold);
            var i = 0;
            foreach (var p in GameSession.I.Match.Team(team))
            {
                var y = 0.62f - i * 0.18f;
                var row = UiFactory.Box(col.transform, new Vector2(0.06f, y), new Vector2(0.94f, y + 0.16f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "r");
                var hero = GameContent.GetHero(p.HeroId);
                UiFactory.Label(row.transform, p.Name + "  ·  " + hero.DisplayName, 18, GameTheme.Text, TextAnchor.MiddleLeft);
                UiFactory.Stretch(row.GetComponentInChildren<Text>().rectTransform, 12, 0);
                i++;
            }
        }

        IEnumerator LoadingRoutine()
        {
            var t = 0f;
            while (t < LoadingSeconds)
            {
                t += Time.deltaTime;
                if (_status != null)
                    _status.text = "Entering the fold…  " + Mathf.Clamp01(t / LoadingSeconds).ToString("P0");
                yield return null;
            }

            SceneManager.LoadScene(AppScenes.Battle);
        }

        void CloseOverlay()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (_overlay != null)
            {
                Destroy(_overlay);
                _overlay = null;
            }
            _status = null;
            _timer = null;
            _slotLabels = null;
            _heroCards = null;
            _lockBtn = null;
        }

        void OnDestroy()
        {
            CloseOverlay();
        }
    }
}
