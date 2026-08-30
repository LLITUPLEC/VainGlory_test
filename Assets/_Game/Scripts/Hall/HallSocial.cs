using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Hall: друзья, пати (до 3) и чат пати.</summary>
    public sealed class HallSocial : MonoBehaviour
    {
        Transform _friendsCol;
        Transform _partyCol;
        Text _chatLabel;
        Text _status;
        Text _inviteLabel;
        GameObject _inviteBar;
        InputField _addField;
        InputField _chatField;
        Coroutine _refresh;

        public static void Open(MonoBehaviour host)
        {
            var canvas = AppUi.OverlayCanvas("SocialOverlay");
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.78f), "Dim");
            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");

            var titleBox = UiFactory.Box(sheet.transform, new Vector2(0.03f, 0.90f), new Vector2(0.72f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "Title");
            UiFactory.Label(titleBox.transform, Loc.T("social.title"), 28, GameTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);

            var close = UiFactory.Button(sheet.transform, Loc.T("social.close"), () => Object.Destroy(canvas.gameObject), GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(close.GetComponent<RectTransform>(), new Vector2(0.80f, 0.90f), new Vector2(0.97f, 0.98f), new Vector2(8, 8), new Vector2(-8, -8));
            close.GetComponentInChildren<Text>().fontSize = 16;

            if (!NakamaConfig.UseServer)
            {
                UiFactory.Label(
                    UiFactory.Box(sheet.transform, new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.70f), Vector2.zero, Vector2.zero, Color.clear, "Dev").transform,
                    Loc.T("social.need_nakama"),
                    20, GameTheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, true);
                return;
            }

            var view = canvas.gameObject.AddComponent<HallSocial>();
            view.Build(host, sheet);
        }

        void Build(MonoBehaviour host, Image sheet)
        {
            var profile = GameSession.I != null ? GameSession.I.Profile : null;
            var username = profile != null && !string.IsNullOrEmpty(profile.Username)
                ? profile.Username
                : (GameSession.I != null && GameSession.I.Nakama != null && GameSession.I.Nakama.Session != null
                    ? GameSession.I.Nakama.Session.Username
                    : "");

            var you = UiFactory.Box(sheet.transform, new Vector2(0.03f, 0.84f), new Vector2(0.72f, 0.90f), Vector2.zero, Vector2.zero, Color.clear, "You");
            UiFactory.Label(you.transform, Loc.T("social.you", username), 16, GameTheme.TextMuted, TextAnchor.MiddleLeft);

            var leftHead = UiFactory.Box(sheet.transform, new Vector2(0.03f, 0.76f), new Vector2(0.48f, 0.83f), Vector2.zero, Vector2.zero, Color.clear, "FH");
            UiFactory.Label(leftHead.transform, Loc.T("social.friends"), 18, GameTheme.Teal, TextAnchor.MiddleLeft, FontStyle.Bold);

            _addField = UiFactory.Input(sheet.transform, Loc.T("social.username_ph"), 64);
            UiFactory.SetAnchors(_addField.GetComponent<RectTransform>(), new Vector2(0.03f, 0.68f), new Vector2(0.32f, 0.76f), Vector2.zero, Vector2.zero);
            _addField.GetComponentInChildren<Text>().fontSize = 16;

            var add = UiFactory.Button(sheet.transform, Loc.T("social.add"), () =>
            {
                host.StartCoroutine(Run(_addField.text, s => s.AddByUsernameAsync(_addField.text)));
            }, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(add.GetComponent<RectTransform>(), new Vector2(0.33f, 0.68f), new Vector2(0.48f, 0.76f), new Vector2(6, 4), new Vector2(-4, -4));
            add.GetComponentInChildren<Text>().fontSize = 16;

            var friendsBox = UiFactory.Box(sheet.transform, new Vector2(0.03f, 0.10f), new Vector2(0.48f, 0.67f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "Friends");
            _friendsCol = friendsBox.transform;

            var rightHead = UiFactory.Box(sheet.transform, new Vector2(0.52f, 0.76f), new Vector2(0.72f, 0.83f), Vector2.zero, Vector2.zero, Color.clear, "PH");
            UiFactory.Label(rightHead.transform, Loc.T("social.party"), 18, GameTheme.Teal, TextAnchor.MiddleLeft, FontStyle.Bold);

            var create = UiFactory.Button(sheet.transform, Loc.T("social.create"), () =>
            {
                host.StartCoroutine(Run("", s => s.CreatePartyAsync()));
            }, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(create.GetComponent<RectTransform>(), new Vector2(0.73f, 0.76f), new Vector2(0.85f, 0.83f), new Vector2(4, 4), new Vector2(-4, -4));
            create.GetComponentInChildren<Text>().fontSize = 14;

            var leave = UiFactory.Button(sheet.transform, Loc.T("social.leave"), () =>
            {
                host.StartCoroutine(Run("", s => s.LeavePartyAsync()));
            }, GameTheme.Crimson, GameTheme.Text);
            UiFactory.SetAnchors(leave.GetComponent<RectTransform>(), new Vector2(0.86f, 0.76f), new Vector2(0.97f, 0.83f), new Vector2(4, 4), new Vector2(-4, -4));
            leave.GetComponentInChildren<Text>().fontSize = 14;

            _inviteBar = UiFactory.Box(sheet.transform, new Vector2(0.52f, 0.68f), new Vector2(0.97f, 0.75f), Vector2.zero, Vector2.zero, GameTheme.Hex(0xD4B45A, 0.22f), "Invite").gameObject;
            _inviteLabel = UiFactory.Label(_inviteBar.transform, Loc.T("social.invited"), 14, GameTheme.Gold, TextAnchor.MiddleLeft);
            UiFactory.Stretch(_inviteLabel.rectTransform, 10, 2);
            var join = UiFactory.Button(_inviteBar.transform, Loc.T("social.join"), () =>
            {
                var id = Soc() != null ? Soc().PendingInvite : "";
                host.StartCoroutine(Run(id, s => s.JoinPartyAsync(id)));
            }, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(join.GetComponent<RectTransform>(), new Vector2(0.58f, 0.12f), new Vector2(0.76f, 0.88f), Vector2.zero, Vector2.zero);
            join.GetComponentInChildren<Text>().fontSize = 12;
            var dismiss = UiFactory.Button(_inviteBar.transform, Loc.T("social.dismiss"), () =>
            {
                if (Soc() != null)
                    Soc().DismissInvite();
            }, GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(dismiss.GetComponent<RectTransform>(), new Vector2(0.78f, 0.12f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
            dismiss.GetComponentInChildren<Text>().fontSize = 12;

            var partyBox = UiFactory.Box(sheet.transform, new Vector2(0.52f, 0.46f), new Vector2(0.97f, 0.67f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "Party");
            _partyCol = partyBox.transform;

            var chatBox = UiFactory.Box(sheet.transform, new Vector2(0.52f, 0.18f), new Vector2(0.97f, 0.45f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "Chat");
            _chatLabel = UiFactory.Label(chatBox.transform, "", 14, GameTheme.Text, TextAnchor.UpperLeft, FontStyle.Normal, true);
            UiFactory.Stretch(_chatLabel.rectTransform, 10, 8);

            _chatField = UiFactory.Input(sheet.transform, Loc.T("social.chat_ph"), 80);
            UiFactory.SetAnchors(_chatField.GetComponent<RectTransform>(), new Vector2(0.52f, 0.10f), new Vector2(0.82f, 0.17f), Vector2.zero, Vector2.zero);
            _chatField.GetComponentInChildren<Text>().fontSize = 16;

            var send = UiFactory.Button(sheet.transform, Loc.T("social.send"), () =>
            {
                var text = _chatField.text;
                _chatField.text = "";
                host.StartCoroutine(Run(text, s => s.SendChatAsync(text)));
            }, GameTheme.Teal, GameTheme.Bg);
            UiFactory.SetAnchors(send.GetComponent<RectTransform>(), new Vector2(0.83f, 0.10f), new Vector2(0.97f, 0.17f), new Vector2(4, 2), new Vector2(-4, -2));
            send.GetComponentInChildren<Text>().fontSize = 16;

            var statusBox = UiFactory.Box(sheet.transform, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.09f), Vector2.zero, Vector2.zero, Color.clear, "St");
            _status = UiFactory.Label(statusBox.transform, "", 14, GameTheme.Crimson, TextAnchor.MiddleLeft);

            KeyboardLift.Attach(sheet.rectTransform);

            var social = Soc();
            if (social != null)
                social.Changed += Refresh;
            _refresh = StartCoroutine(BootRefresh());
            Refresh();
        }

        IEnumerator BootRefresh()
        {
            var social = Soc();
            if (social == null)
                yield break;
            var task = social.RefreshFriendsAsync();
            while (!task.IsCompleted)
                yield return null;
        }

        IEnumerator Run(string _, System.Func<NakamaSocial, System.Threading.Tasks.Task> work)
        {
            var social = Soc();
            if (social == null)
                yield break;
            var task = work(social);
            while (!task.IsCompleted)
                yield return null;
        }

        void OnDestroy()
        {
            var social = Soc();
            if (social != null)
                social.Changed -= Refresh;
            if (_refresh != null)
                StopCoroutine(_refresh);
        }

        void Refresh()
        {
            if (!isActiveAndEnabled || _friendsCol == null)
                return;
            var social = Soc();
            if (social == null)
                return;
            _status.text = social.Status;
            if (_inviteBar != null)
                _inviteBar.SetActive(!string.IsNullOrEmpty(social.PendingInvite));

            ClearKids(_friendsCol);
            var friends = social.Friends;
            if (friends.Count == 0)
            {
                UiFactory.Label(_friendsCol, Loc.T("social.empty"), 16, GameTheme.TextMuted, TextAnchor.MiddleCenter);
            }
            else
            {
                var shown = 0;
                foreach (var friend in friends)
                {
                    if (shown >= 7 || friend == null || friend.User == null)
                        continue;
                    DrawFriend(friend, shown);
                    shown++;
                }
            }

            ClearKids(_partyCol);
            if (!social.InParty)
            {
                UiFactory.Label(_partyCol, Loc.T("social.party_empty"), 16, GameTheme.TextMuted, TextAnchor.MiddleCenter);
            }
            else
            {
                var me = GameSession.I != null && GameSession.I.Nakama != null && GameSession.I.Nakama.Session != null
                    ? GameSession.I.Nakama.Session.UserId
                    : "";
                var members = social.MembersSnapshot();
                for (var i = 0; i < members.Length && i < 3; i++)
                    DrawMember(members[i], i, me, social);
            }

            if (_chatLabel != null)
            {
                var lines = social.ChatLines;
                var text = "";
                var start = Mathf.Max(0, lines.Count - 8);
                for (var i = start; i < lines.Count; i++)
                    text += lines[i] + "\n";
                _chatLabel.text = text;
            }
        }

        void DrawFriend(Nakama.IApiFriend friend, int index)
        {
            var y = 0.86f - index * 0.12f;
            var row = UiFactory.Box(_friendsCol, new Vector2(0.03f, y - 0.10f), new Vector2(0.97f, y), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "F" + index);
            var stateKey = NakamaSocial.FriendStateKey(friend);
            var caption = NakamaSocial.FriendLabel(friend);
            if (!string.IsNullOrEmpty(stateKey))
                caption += "  " + Loc.T(stateKey);
            var nameLabel = UiFactory.Label(row.transform, caption, 14, GameTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.SetAnchors(nameLabel.rectTransform, Vector2.zero, new Vector2(0.50f, 1f), new Vector2(8, 2), new Vector2(-4, -2));

            var uid = friend.User.Id;
            var social = Soc();
            if (friend.State == 2)
                Mini(row.transform, 0.52f, Loc.T("social.accept"), () => StartCoroutine(Run(uid, s => s.AcceptAsync(uid))), GameTheme.Gold, GameTheme.Bg);
            else if (friend.State == 0 && social != null && social.InParty && social.PartySize < NakamaSocial.PartyMax)
                Mini(row.transform, 0.52f, Loc.T("social.invite"), () => StartCoroutine(Run(uid, s => s.InviteFriendAsync(uid))), GameTheme.Teal, GameTheme.Bg);

            if (friend.State == 0)
                Mini(row.transform, 0.84f, "✕", () => AskRemove(uid), GameTheme.Crimson, GameTheme.Text);
            else
                Mini(row.transform, 0.84f, "✕", () => StartCoroutine(Run(uid, s => s.RemoveAsync(uid))), GameTheme.Crimson, GameTheme.Text);
        }

        void DrawMember(Nakama.IUserPresence p, int index, string me, NakamaSocial social)
        {
            if (p == null)
                return;
            var y = 0.70f - index * 0.28f;
            var row = UiFactory.Box(_partyCol, new Vector2(0.04f, y), new Vector2(0.96f, y + 0.24f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "M" + index);
            var tag = "";
            if (p.UserId == social.LeaderId)
                tag = Loc.T("social.leader") + "  ";
            else if (p.UserId == me)
                tag = Loc.T("social.you_tag");
            var name = string.IsNullOrEmpty(p.Username) ? p.UserId : p.Username;
            UiFactory.Label(row.transform, tag + name, 16, GameTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.Stretch(row.GetComponentInChildren<Text>().rectTransform, 10, 4);

            if (social.IsLeader && p.UserId != me)
            {
                var presence = p;
                Mini(row.transform, 0.84f, "✕", () => StartCoroutine(Run("", s => s.KickAsync(presence))), GameTheme.Crimson, GameTheme.Text);
            }
        }

        void AskRemove(string uid)
        {
            var canvas = AppUi.OverlayCanvas("FriendRemoveConfirm", 50);
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.55f), "Dim");
            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.66f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            UiFactory.Label(
                UiFactory.Box(sheet.transform, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.88f), Vector2.zero, Vector2.zero, Color.clear, "Msg").transform,
                Loc.T("social.remove_confirm"),
                20, GameTheme.Text, TextAnchor.MiddleCenter, FontStyle.Normal, true);

            var yes = UiFactory.Button(sheet.transform, Loc.T("social.yes"), () =>
            {
                Object.Destroy(canvas.gameObject);
                StartCoroutine(Run(uid, s => s.RemoveAsync(uid)));
            }, GameTheme.Crimson, GameTheme.Text);
            UiFactory.SetAnchors(yes.GetComponent<RectTransform>(), new Vector2(0.08f, 0.10f), new Vector2(0.48f, 0.38f), new Vector2(6, 4), new Vector2(-6, -4));
            yes.GetComponentInChildren<Text>().fontSize = 18;

            var no = UiFactory.Button(sheet.transform, Loc.T("social.no"), () => Object.Destroy(canvas.gameObject), GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(no.GetComponent<RectTransform>(), new Vector2(0.52f, 0.10f), new Vector2(0.92f, 0.38f), new Vector2(6, 4), new Vector2(-6, -4));
            no.GetComponentInChildren<Text>().fontSize = 18;
        }

        static void Mini(Transform parent, float x, string caption, UnityEngine.Events.UnityAction action, Color bg, Color fg)
        {
            var btn = UiFactory.Button(parent, caption, action, bg, fg);
            UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(x, 0.12f), new Vector2(x + 0.14f, 0.88f), Vector2.zero, Vector2.zero);
            btn.GetComponentInChildren<Text>().fontSize = 12;
        }

        static void ClearKids(Transform t)
        {
            if (t == null)
                return;
            for (var i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        static NakamaSocial Soc()
        {
            return GameSession.I != null ? GameSession.I.Social : null;
        }
    }
}
