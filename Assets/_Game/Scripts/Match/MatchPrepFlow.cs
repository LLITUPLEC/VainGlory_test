using System.Collections;
using System.Collections.Generic;
using Nakama;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>PLAY → режим → очередь → драфт → loading. Nakama-комната держится до Results.</summary>
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
        bool _queueCancelled;
        IMatchmakerMatched _matched;
        bool _partyMemberWait;

        public static bool Queuing { get; private set; }

        public void OpenModeSelect()
        {
            CloseOverlay();
            var canvas = AppUi.OverlayCanvas("ModeOverlay");
            _overlay = canvas.gameObject;
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.78f), "Dim");

            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.28f, 0.22f), new Vector2(0.72f, 0.78f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            UiFactory.Label(sheet.transform, Loc.T("mode.title"), 28, GameTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);

            var casual = UiFactory.Button(sheet.transform, Loc.T("mode.casual_btn"), StartCasualQueue, GameTheme.Gold, GameTheme.Bg);
            if (NakamaConfig.UseServer)
            {
                UiFactory.SetAnchors(casual.GetComponent<RectTransform>(), new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.74f), Vector2.zero, Vector2.zero);
                casual.GetComponentInChildren<Text>().fontSize = 28;
                var solo = UiFactory.Button(sheet.transform, Loc.T("mode.solo_btn"), StartLocalQueue, GameTheme.BgPanelSoft, GameTheme.Text);
                UiFactory.SetAnchors(solo.GetComponent<RectTransform>(), new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.50f), Vector2.zero, Vector2.zero);
                solo.GetComponentInChildren<Text>().fontSize = 22;
            }
            else
            {
                UiFactory.SetAnchors(casual.GetComponent<RectTransform>(), new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.68f), Vector2.zero, Vector2.zero);
                casual.GetComponentInChildren<Text>().fontSize = 32;
            }

            var hint = UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.34f), Vector2.zero, Vector2.zero, Color.clear, "Hint");
            UiFactory.Label(hint.transform, Loc.T(NakamaConfig.UseServer ? "mode.hint_nakama" : "mode.hint"), 16, GameTheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, true);

            var cancel = UiFactory.Button(sheet.transform, Loc.T("mode.back"), CloseOverlay, GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.18f), Vector2.zero, Vector2.zero);
            cancel.GetComponentInChildren<Text>().fontSize = 20;
        }

        void StartCasualQueue()
        {
            var social = GameSession.I != null ? GameSession.I.Social : null;
            if (NakamaConfig.UseServer && social != null && social.InParty && !social.IsLeader)
            {
                ShowPartyHint(Loc.T("social.leader_queues"));
                return;
            }
            if (NakamaConfig.UseServer)
                StartNakamaQueue();
            else
                StartLocalQueue();
        }

        void ShowPartyHint(string msg)
        {
            CloseOverlay();
            var canvas = AppUi.OverlayCanvas("PartyHint");
            _overlay = canvas.gameObject;
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.78f), "Dim");
            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.28f, 0.36f), new Vector2(0.72f, 0.64f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            UiFactory.Label(sheet.transform, msg, 20, GameTheme.Text, TextAnchor.MiddleCenter, FontStyle.Normal, true);
            var ok = UiFactory.Button(sheet.transform, Loc.T("social.close"), CloseOverlay, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(ok.GetComponent<RectTransform>(), new Vector2(0.25f, 0.10f), new Vector2(0.75f, 0.32f), Vector2.zero, Vector2.zero);
            ok.GetComponentInChildren<Text>().fontSize = 18;
        }

        void StartLocalQueue()
        {
            CloseOverlay();
            BuildQueue();
            _routine = StartCoroutine(LocalQueueRoutine());
        }

        void StartNakamaQueue()
        {
            CloseOverlay();
            BuildQueue();
            _routine = StartCoroutine(NakamaQueueRoutine());
        }

        IEnumerator LocalQueueRoutine()
        {
            var t = 0f;
            while (t < QueueSeconds)
            {
                t += Time.deltaTime;
                var sec = Mathf.FloorToInt(t);
                if (_status != null)
                    _status.text = Loc.T("queue.filling", sec);
                yield return null;
            }

            GameSession.I.Match = CreateLocalMatch();
            ShowMatchFound();
            yield return new WaitForSeconds(FoundSeconds);
            OpenDraft();
        }

        IEnumerator NakamaQueueRoutine()
        {
            Queuing = true;
            _queueCancelled = false;
            _matched = null;
            _partyMemberWait = false;
            var nk = GameSession.I.Nakama;
            var social = GameSession.I.Social;
            if (_status != null)
                _status.text = Loc.T("queue.connecting");

            var connect = nk.ConnectRealtimeAsync();
            while (!connect.IsCompleted)
                yield return null;
            if (_queueCancelled)
                yield break;
            if (connect.IsFaulted)
            {
                ShowQueueError(connect.Exception);
                yield break;
            }

            nk.Socket.ReceivedMatchmakerMatched += OnMatched;

            var partyQueue = social != null && social.InParty && social.IsLeader && social.PartySize >= 2;
            System.Threading.Tasks.Task add;
            if (partyQueue)
            {
                if (_status != null)
                    _status.text = Loc.T("social.queue_party");
                add = nk.AddMatchmakerPartyAsync(social.PartyId, social.PartySize);
                var cue = social.NotifyQueueAsync(true);
                while (!cue.IsCompleted)
                    yield return null;
            }
            else
                add = nk.AddMatchmakerAsync();
            while (!add.IsCompleted)
                yield return null;
            if (_queueCancelled)
            {
                UnhookMatchmaker();
                yield break;
            }
            if (add.IsFaulted)
            {
                UnhookMatchmaker();
                ShowQueueError(add.Exception);
                yield break;
            }

            yield return WaitJoinAndDraft(nk);
        }

        public void JoinIncomingMatch(IMatchmakerMatched matched)
        {
            if (matched == null)
                return;
            _matched = matched;
            if (Queuing)
                return;
            CloseOverlay();
            BuildQueue();
            _routine = StartCoroutine(JoinIncomingRoutine());
        }

        public void WaitAsPartyMember()
        {
            if (Queuing)
                return;
            CloseOverlay();
            BuildQueue();
            if (_status != null)
                _status.text = Loc.T("social.queue_party");
            _routine = StartCoroutine(PartyMemberWaitRoutine());
        }

        public void CancelFromPartyLeader()
        {
            if (!Queuing || !_partyMemberWait)
                return;
            _queueCancelled = true;
            Queuing = false;
            UnhookMatchmaker();
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            CloseOverlay();
        }

        IEnumerator JoinIncomingRoutine()
        {
            Queuing = true;
            _queueCancelled = false;
            _partyMemberWait = false;
            var nk = GameSession.I.Nakama;
            if (nk.Socket != null)
                nk.Socket.ReceivedMatchmakerMatched += OnMatched;
            yield return WaitJoinAndDraft(nk);
        }

        IEnumerator PartyMemberWaitRoutine()
        {
            Queuing = true;
            _queueCancelled = false;
            _partyMemberWait = true;
            var nk = GameSession.I.Nakama;
            if (nk != null && nk.Socket != null)
                nk.Socket.ReceivedMatchmakerMatched += OnMatched;
            yield return WaitJoinAndDraft(nk);
        }

        IEnumerator WaitJoinAndDraft(NakamaConnection nk)
        {
            if (nk == null)
            {
                Queuing = false;
                yield break;
            }
            var t = 0f;
            while (_matched == null && !_queueCancelled)
            {
                t += Time.deltaTime;
                if (_status != null && !_partyMemberWait)
                    _status.text = Loc.T("queue.waiting", Mathf.FloorToInt(t));
                yield return null;
            }

            if (_queueCancelled)
            {
                UnhookMatchmaker();
                yield break;
            }

            if (_status != null)
                _status.text = Loc.T("queue.joining");
            var join = nk.JoinMatchedAsync(_matched);
            while (!join.IsCompleted)
                yield return null;
            if (_queueCancelled)
            {
                UnhookMatchmaker();
                yield break;
            }
            if (join.IsFaulted)
            {
                UnhookMatchmaker();
                ShowQueueError(join.Exception);
                yield break;
            }

            UnhookMatchmaker();
            var wait = 0f;
            var mc = GameSession.I.MatchClient;
            while ((mc == null || mc.Roster == null) && wait < 8f && !_queueCancelled)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            if (_queueCancelled)
                yield break;

            ApplyLiveRoster();
            Debug.Log("[Ashfold] Match room " + (nk.CurrentMatch != null ? nk.CurrentMatch.Id : "") +
                      " humans=" + (GameSession.I.Match != null ? GameSession.I.Match.Players.Count : 0));

            Queuing = false;
            ShowMatchFound();
            yield return new WaitForSeconds(FoundSeconds);
            OpenDraft();
        }

        void OnMatched(IMatchmakerMatched matched)
        {
            _matched = matched;
        }

        void UnhookMatchmaker()
        {
            var socket = GameSession.I != null && GameSession.I.Nakama != null
                ? GameSession.I.Nakama.Socket
                : null;
            if (socket == null)
                return;
            socket.ReceivedMatchmakerMatched -= OnMatched;
        }

        void ApplyLiveRoster()
        {
            var nk = GameSession.I != null ? GameSession.I.Nakama : null;
            var mc = GameSession.I != null ? GameSession.I.MatchClient : null;
            var localId = GameSession.I.Profile != null ? GameSession.I.Profile.UserId : "";
            var matchId = nk != null && nk.CurrentMatch != null ? nk.CurrentMatch.Id : "";
            var roster = mc != null ? mc.Roster : null;
            if (roster == null)
                roster = FallbackRoster(nk != null ? nk.CurrentMatch : null, localId);
            GameSession.I.Match = MatchRoster.FromNakama(roster, localId, matchId);
        }

        void ShowQueueError(System.AggregateException ex)
        {
            Queuing = false;
            var msg = ex != null && ex.GetBaseException() != null ? ex.GetBaseException().Message : "queue failed";
            Debug.LogError("[Ashfold] Queue failed: " + msg);
            if (_status != null)
                _status.text = Loc.T("queue.failed");
        }

        static NakamaRosterDto FallbackRoster(IMatch match, string localId)
        {
            var list = new List<NakamaRosterPlayer>();
            var seen = new HashSet<string>();
            if (match != null)
            {
                if (match.Self != null && seen.Add(match.Self.UserId))
                    list.Add(PresencePlayer(match.Self, list.Count));
                if (match.Presences != null)
                {
                    foreach (var p in match.Presences)
                    {
                        if (p != null && seen.Add(p.UserId))
                            list.Add(PresencePlayer(p, list.Count));
                    }
                }
            }
            if (list.Count == 0 && !string.IsNullOrEmpty(localId))
            {
                var name = GameSession.I.Profile != null ? GameSession.I.Profile.DisplayName : "Player";
                list.Add(new NakamaRosterPlayer { userId = localId, username = name, team = 0, slot = 0 });
            }
            return new NakamaRosterDto { count = list.Count, players = list.ToArray() };
        }

        static NakamaRosterPlayer PresencePlayer(IUserPresence p, int index)
        {
            return new NakamaRosterPlayer
            {
                userId = p.UserId,
                username = p.Username,
                team = index % 2,
                slot = index / 2
            };
        }

        void BuildQueue()
        {
            var canvas = AppUi.OverlayCanvas("QueueOverlay");
            _overlay = canvas.gameObject;
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.82f), "Dim");

            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.22f, 0.28f), new Vector2(0.78f, 0.72f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            UiFactory.Label(sheet.transform, Loc.T("queue.searching"), 30, GameTheme.Gold, TextAnchor.UpperCenter, FontStyle.Bold);

            var statusBox = UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.62f), Vector2.zero, Vector2.zero, Color.clear, "Status");
            _status = UiFactory.Label(statusBox.transform, Loc.T("queue.status", 0), 24, GameTheme.Teal, TextAnchor.MiddleCenter);

            var cancel = UiFactory.Button(sheet.transform, Loc.T("queue.cancel"), CancelQueue, GameTheme.Crimson, GameTheme.Text);
            UiFactory.SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.25f, 0.10f), new Vector2(0.75f, 0.28f), Vector2.zero, Vector2.zero);
        }

        void ShowMatchFound()
        {
            if (_status != null)
                _status.text = Loc.T("queue.found");
        }

        void CancelQueue()
        {
            _queueCancelled = true;
            Queuing = false;
            UnhookMatchmaker();
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            GameSession.I.Match = null;
            var social = GameSession.I != null ? GameSession.I.Social : null;
            var nk = GameSession.I != null ? GameSession.I.Nakama : null;
            if (social != null && social.InParty && social.IsLeader)
            {
                var _ = social.NotifyQueueAsync(false);
                if (nk != null)
                {
                    var __ = nk.CancelMatchmakerAsync();
                }
            }
            else if (social != null && social.InParty)
            {
                var _ = social.LeavePartyAsync();
            }
            else if (nk != null)
            {
                var _ = nk.CancelMatchmakerAsync();
            }
            CloseOverlay();
        }

        static MatchSession CreateLocalMatch()
        {
            var match = new MatchSession();
            var me = GameSession.I.Profile != null ? GameSession.I.Profile.DisplayName : "Player";
            var uid = GameSession.I.Profile != null ? GameSession.I.Profile.UserId : "";
            match.Players.Add(new MatchParticipant { UserId = uid, Name = me, IsLocal = true, Team = 0, Slot = 0 });

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
                Loc.T("draft.title"), 26, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            _timer = UiFactory.Label(
                UiFactory.Box(canvas.transform, new Vector2(0.42f, 0.82f), new Vector2(0.58f, 0.90f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Timer").transform,
                "0:20", 28, GameTheme.Teal, TextAnchor.MiddleCenter, FontStyle.Bold);

            _slotLabels = new Text[6];
            BuildTeamColumn(canvas.transform, 0, 0.02f, Loc.T("draft.dawn"));
            BuildTeamColumn(canvas.transform, 1, 0.70f, Loc.T("draft.dusk"));

            _heroCards = new Image[GameContent.Heroes.Length];
            for (var i = 0; i < GameContent.Heroes.Length; i++)
            {
                var hero = GameContent.Heroes[i];
                var unlocked = GameSession.I == null || GameSession.I.Profile == null || GameSession.I.Profile.IsHeroUnlocked(hero.Id);
                var x0 = 0.28f + i * 0.15f;
                var btn = UiFactory.Button(canvas.transform, hero.DisplayName.ToUpperInvariant(), () => SelectHero(hero.Id), GameTheme.BgPanel, GameTheme.Text);
                UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(x0, 0.30f), new Vector2(x0 + 0.14f, 0.72f), Vector2.zero, Vector2.zero);
                btn.GetComponentInChildren<Text>().fontSize = 18;
                btn.interactable = unlocked;
                _heroCards[i] = btn.GetComponent<Image>();
                var swatch = UiFactory.Box(btn.transform, new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.92f), Vector2.zero, Vector2.zero, unlocked ? GameContent.HeroColor(hero.Id) : GameTheme.TextMuted, "C");
                swatch.raycastTarget = false;
            }

            _lockBtn = UiFactory.Button(canvas.transform, Loc.T("draft.lock"), LockIn, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(_lockBtn.GetComponent<RectTransform>(), new Vector2(0.38f, 0.08f), new Vector2(0.62f, 0.18f), Vector2.zero, Vector2.zero);

            var stage = UiFactory.Box(canvas.transform, new Vector2(0.02f, 0.01f), new Vector2(0.3f, 0.06f), Vector2.zero, Vector2.zero, Color.clear, "St");
            UiFactory.Label(stage.transform, Loc.T("draft.stage"), 14, GameTheme.GoldDim, TextAnchor.MiddleLeft);

            SelectHero(_pickedId);
            RefreshSlots();
            if (GameSession.I != null && GameSession.I.Match != null && GameSession.I.Match.IsNetworked)
                _routine = StartCoroutine(NetDraftRoutine());
            else
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
            if (GameSession.I != null && GameSession.I.Profile != null && !GameSession.I.Profile.IsHeroUnlocked(id))
                return;
            _pickedId = id;
            for (var i = 0; i < GameContent.Heroes.Length; i++)
            {
                var selected = GameContent.Heroes[i].Id == id;
                _heroCards[i].color = selected ? GameTheme.GoldDim : GameTheme.BgPanel;
            }
            if (NetDraft())
            {
                var local = GameSession.I.Match.Local;
                if (local != null && !local.Locked)
                    local.HeroId = id;
                GameSession.I.MatchClient.SendPick(id);
                RefreshSlots();
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
            _lockBtn.GetComponentInChildren<Text>().text = Loc.T("draft.locked");
            if (NetDraft())
                GameSession.I.MatchClient.SendLock(_pickedId);
            RefreshSlots();
        }

        static bool NetDraft()
        {
            return GameSession.I != null
                   && GameSession.I.Match != null
                   && GameSession.I.Match.IsNetworked
                   && GameSession.I.MatchClient != null;
        }

        IEnumerator NetDraftRoutine()
        {
            if (!_locked)
                SelectHero(_pickedId);

            while (true)
            {
                ApplyLiveRoster();
                var local = GameSession.I.Match != null ? GameSession.I.Match.Local : null;
                if (local != null && local.Locked && !_locked)
                {
                    _locked = true;
                    if (_lockBtn != null)
                    {
                        _lockBtn.interactable = false;
                        _lockBtn.GetComponentInChildren<Text>().text = Loc.T("draft.locked");
                    }
                }
                var mc = GameSession.I.MatchClient;
                var phase = mc != null ? mc.Phase : "";
                if (_timer != null)
                    _timer.text = "0:" + Mathf.CeilToInt(Mathf.Max(0f, mc != null ? mc.DraftLeft : 0f)).ToString("00");
                RefreshSlots();

                if (phase == "loading" || phase == "combat" || phase == "ended")
                    break;
                yield return null;
            }

            if (!_locked)
                LockIn();
            yield return new WaitForSeconds(0.4f);
            OpenLoading();
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
            foreach (var p in GameSession.I.Match.Players)
            {
                if (!p.IsBot && !p.IsLocal && string.IsNullOrEmpty(p.HeroId))
                    p.HeroId = "bastion";
            }
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
                if (p.IsLocal && !p.Locked)
                    return false;
                if (p.IsBot && !p.Locked)
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
                var hero = string.IsNullOrEmpty(p.HeroId) ? Loc.T("draft.picking") : GameContent.GetHero(p.HeroId).DisplayName.ToUpperInvariant();
                var tag = p.IsLocal ? Loc.T("draft.you") : p.IsBot ? Loc.T("draft.bot") : "";
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
                Loc.T("loading.map"), 36, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            UiFactory.Label(
                UiFactory.Box(canvas.transform, new Vector2(0.15f, 0.70f), new Vector2(0.85f, 0.78f), Vector2.zero, Vector2.zero, Color.clear, "M").transform,
                Loc.T("loading.mode"), 16, GameTheme.TextMuted, TextAnchor.MiddleCenter);

            DrawLoadingTeam(canvas.transform, 0, 0.06f, Loc.T("draft.dawn"));
            DrawLoadingTeam(canvas.transform, 1, 0.52f, Loc.T("draft.dusk"));

            _status = UiFactory.Label(
                UiFactory.Box(canvas.transform, new Vector2(0.2f, 0.08f), new Vector2(0.8f, 0.16f), Vector2.zero, Vector2.zero, Color.clear, "L").transform,
                Loc.T("loading.entering"), 20, GameTheme.Teal, TextAnchor.MiddleCenter);

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
                    _status.text = Loc.T("loading.entering_pct", Mathf.Clamp01(t / LoadingSeconds));
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
            Queuing = false;
            CloseOverlay();
        }
    }
}
