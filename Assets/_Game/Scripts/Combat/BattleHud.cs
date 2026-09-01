using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Ashfold
{
    public sealed class BattleHud : MonoBehaviour
    {
        public CombatUnit Player;
        public HeroCombat Combat;
        Text _hp;
        Text _gold;
        Text _kda;
        Text _items;
        Text _hint;
        Text _death;
        Text _clock;
        readonly SkillSlot[] _skills = new SkillSlot[HeroRules.SlotCount];
        Button _surrender;
        Image _recallBar;
        GameObject _deathPanel;
        GameObject _netPanel;
        Text _netText;
        GameObject _countPanel;
        Text _countText;
        bool _dead;

        public static BattleHud Create(CombatUnit player, HeroCombat combat)
        {
            var canvas = UiFactory.CreateCanvas("BattleHud");
            canvas.sortingOrder = 20;
            var hud = canvas.gameObject.AddComponent<BattleHud>();
            hud.Player = player;
            hud.Combat = combat;
            hud.Build(canvas.transform);
            return hud;
        }

        public void SetSurrender(UnityAction action)
        {
            if (_surrender != null)
            {
                _surrender.onClick.RemoveAllListeners();
                _surrender.onClick.AddListener(action);
            }
        }

        public void SetHint(string text)
        {
            if (_hint != null)
                _hint.text = text;
        }

        public void SetNetStatus(string text, bool show)
        {
            if (_netPanel != null)
                _netPanel.SetActive(show);
            if (show && _netText != null)
                _netText.text = text;
        }

        public void SetDeathTimer(float seconds)
        {
            _dead = true;
            if (_deathPanel != null)
                _deathPanel.SetActive(true);
            if (_death != null)
                _death.text = Loc.T("hud.respawn", Mathf.CeilToInt(seconds));
        }

        public void ClearDeathTimer()
        {
            _dead = false;
            if (_deathPanel != null)
                _deathPanel.SetActive(false);
        }

        void Build(Transform root)
        {
            WorldClickCatcher.Attach(root, Combat != null ? Combat.GetComponent<PlayerCommander>() : null);

            var top = UiFactory.Box(root, new Vector2(0.02f, 0.82f), new Vector2(0.30f, 0.98f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Hero");
            _hp = UiFactory.Label(top.transform, "", 16, GameTheme.Text, TextAnchor.MiddleLeft, FontStyle.Normal, true);
            UiFactory.Stretch(_hp.rectTransform, 12, 4);

            var gold = UiFactory.Box(root, new Vector2(0.78f, 0.92f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Gold");
            _gold = UiFactory.Label(gold.transform, "0 G", 20, GameTheme.Gold, TextAnchor.MiddleRight);
            UiFactory.Stretch(_gold.rectTransform, 12, 0);

            var clock = UiFactory.Box(root, new Vector2(0.42f, 0.92f), new Vector2(0.58f, 0.98f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "Clock");
            _clock = UiFactory.Label(clock.transform, "0:00", 18, GameTheme.Teal, TextAnchor.MiddleCenter, FontStyle.Bold);

            var kda = UiFactory.Box(root, new Vector2(0.78f, 0.85f), new Vector2(0.98f, 0.91f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "Kda");
            _kda = UiFactory.Label(kda.transform, "0 / 0 / 0", 16, GameTheme.TextMuted, TextAnchor.MiddleRight);
            UiFactory.Stretch(_kda.rectTransform, 12, 0);

            var items = UiFactory.Box(root, new Vector2(0.78f, 0.76f), new Vector2(0.98f, 0.84f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "Items");
            _items = UiFactory.Label(items.transform, Loc.T("hud.items_empty"), 14, GameTheme.TextMuted, TextAnchor.MiddleRight, FontStyle.Normal, true);
            UiFactory.Stretch(_items.rectTransform, 10, 2);

            MinimapView.Create(root);

            var bar = UiFactory.Box(root, new Vector2(0.18f, 0.03f), new Vector2(0.40f, 0.14f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Hint");
            _hint = UiFactory.Label(bar.transform, Loc.T("hud.hint"), 15, GameTheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, true);

            _skills[0] = MakeSkill(root, 0, "Q", 0.41f, 0.51f, GameTheme.Gold);
            _skills[1] = MakeSkill(root, 1, "W", 0.52f, 0.62f, GameTheme.Teal);
            _skills[2] = MakeSkill(root, 2, "E", 0.63f, 0.73f, GameTheme.Crimson);

            var shop = UiFactory.Button(root, Loc.T("hud.shop"), OnShop, GameTheme.BgPanelSoft, GameTheme.Gold);
            UiFactory.SetAnchors(shop.GetComponent<RectTransform>(), new Vector2(0.75f, 0.03f), new Vector2(0.85f, 0.14f), Vector2.zero, Vector2.zero);
            shop.GetComponentInChildren<Text>().fontSize = 16;

            var recall = UiFactory.Button(root, Loc.T("hud.recall"), OnRecall, GameTheme.BgPanelSoft, GameTheme.Teal);
            UiFactory.SetAnchors(recall.GetComponent<RectTransform>(), new Vector2(0.86f, 0.03f), new Vector2(0.97f, 0.14f), Vector2.zero, Vector2.zero);
            recall.GetComponentInChildren<Text>().fontSize = 16;

            _surrender = UiFactory.Button(root, Loc.T("hud.surrender"), () => { }, GameTheme.Crimson, GameTheme.Text);
            UiFactory.SetAnchors(_surrender.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.16f, 0.14f), Vector2.zero, Vector2.zero);
            _surrender.GetComponentInChildren<Text>().fontSize = 16;

            var recBg = UiFactory.Box(root, new Vector2(0.35f, 0.18f), new Vector2(0.65f, 0.22f), Vector2.zero, Vector2.zero, GameTheme.Hex(0xFFFFFF, 0.12f), "RecallBar");
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(recBg.transform, false);
            _recallBar = fillGo.GetComponent<Image>();
            _recallBar.color = GameTheme.Teal;
            var rt = fillGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            recBg.gameObject.SetActive(false);

            _deathPanel = UiFactory.Box(root, new Vector2(0.35f, 0.42f), new Vector2(0.65f, 0.58f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x000000, 0.65f), "Death").gameObject;
            _death = UiFactory.Label(_deathPanel.transform, Loc.T("hud.respawn", 5), 32, GameTheme.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            _deathPanel.SetActive(false);

            _netPanel = UiFactory.Box(root, new Vector2(0.25f, 0.40f), new Vector2(0.75f, 0.60f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x000000, 0.72f), "Net").gameObject;
            _netText = UiFactory.Label(_netPanel.transform, Loc.T("hud.reconnecting", 30), 26, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            _netPanel.SetActive(false);

            _countPanel = UiFactory.Box(root, new Vector2(0.30f, 0.38f), new Vector2(0.70f, 0.62f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x000000, 0.45f), "Count").gameObject;
            _countPanel.GetComponent<Image>().raycastTarget = false;
            _countText = UiFactory.Label(_countPanel.transform, "10", 88, GameTheme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _countPanel.SetActive(false);

            var stage = UiFactory.Box(root, new Vector2(0.32f, 0.94f), new Vector2(0.68f, 0.99f), Vector2.zero, Vector2.zero, Color.clear, "St");
            UiFactory.Label(stage.transform, Loc.T("hud.stage"), 14, GameTheme.GoldDim, TextAnchor.MiddleCenter);
        }

        SkillSlot MakeSkill(Transform root, int slot, string key, float x0, float x1, Color fg)
        {
            var slotCopy = slot;
            var btn = UiFactory.Button(root, key, () => OnSkill(slotCopy), GameTheme.BgPanel, fg);
            UiFactory.SetAnchors(btn.GetComponent<RectTransform>(), new Vector2(x0, 0.03f), new Vector2(x1, 0.14f), Vector2.zero, Vector2.zero);
            var label = btn.GetComponentInChildren<Text>();
            label.fontSize = 15;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            var plus = UiFactory.Button(btn.transform, "+", () => OnUpgrade(slotCopy), GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(plus.GetComponent<RectTransform>(), new Vector2(0.62f, 0.62f), new Vector2(1f, 1f), new Vector2(2, 2), new Vector2(-2, -2));
            plus.GetComponentInChildren<Text>().fontSize = 16;
            return new SkillSlot { Btn = btn, Label = label, Plus = plus };
        }

        void OnSkill(int slot)
        {
            if (_dead || Combat == null)
                return;
            if (BattleRuntime.I != null && BattleRuntime.I.InPrep)
                return;
            var cmd = Combat.GetComponent<PlayerCommander>();
            if (cmd != null)
                cmd.PressSkill(slot);
            else
                Combat.TryCastSkill(slot, Combat.AttackTarget, Combat.transform.position);
        }

        void OnUpgrade(int slot)
        {
            if (_dead || Combat == null)
                return;
            Combat.TryUpgrade(slot);
        }

        void OnShop()
        {
            if (_dead || Combat == null)
                return;
            if (!FoldMapBuilder.InFountain(Combat.transform.position, Player.Team))
            {
                _hint.text = Loc.T("hud.shop_fountain");
                return;
            }
            FountainShop.Open(Combat);
        }

        void OnRecall()
        {
            if (!_dead && Combat != null)
                Combat.TryRecall();
        }

        void Update()
        {
            if (Player == null || Combat == null)
                return;
            var def = Combat.Def;
            var prog = Combat.Progress;
            var lv = prog != null ? prog.Level : 1;
            var pts = prog != null ? prog.Unspent : 0;
            if (_dead)
            {
                _hp.text = def.DisplayName.ToUpperInvariant() + "\n" + Loc.T("hud.dead") + "\n" + Loc.T("hud.level", lv);
                _hp.color = GameTheme.TextMuted;
            }
            else
            {
                var ptsLine = pts > 0 ? "  " + Loc.T("hud.skill_pts", pts) : "";
                _hp.text = def.DisplayName.ToUpperInvariant()
                           + "\n" + Mathf.CeilToInt(Player.Hp) + " / " + Mathf.CeilToInt(Player.MaxHp)
                           + "\n" + Loc.T("hud.level", lv) + ptsLine;
                _hp.color = Color.Lerp(GameTheme.AllyHpLow, GameTheme.AllyHp, Player.Hp01);
            }

            RefreshCountdown();

            var assists = 0;
            if (MatchStatsTracker.I != null && MatchStatsTracker.I.ByUnit.TryGetValue(Player, out var row))
                assists = row.Assists;

            if (BattleRuntime.I != null)
            {
                _gold.text = BattleRuntime.I.Gold + " G";
                _kda.text = BattleRuntime.I.Kills + " / " + BattleRuntime.I.Deaths + " / " + assists;
                var t = Mathf.FloorToInt(BattleRuntime.I.MatchTime);
                _clock.text = (t / 60) + ":" + (t % 60).ToString("00");
            }

            if (Combat.Items.Count == 0)
                _items.text = Loc.T("hud.items_empty");
            else
            {
                var names = "";
                foreach (var id in Combat.Items)
                {
                    var item = GameContent.GetItem(id);
                    if (item != null)
                        names += GameContent.ItemName(item) + "  ";
                }
                _items.text = names;
            }

            for (var i = 0; i < _skills.Length; i++)
                RefreshSkill(_skills[i], i);

            var cmd = Combat.GetComponent<PlayerCommander>();
            var aiming = cmd != null && cmd.AimSlot >= 0;

            var recRoot = _recallBar != null ? _recallBar.transform.parent.gameObject : null;
            if (recRoot != null)
            {
                recRoot.SetActive(!_dead && Combat.Recalling);
                if (!_dead && Combat.Recalling)
                {
                    var t = Mathf.Clamp01(Combat.RecallT / HeroCombat.RecallDuration);
                    _recallBar.rectTransform.anchorMax = new Vector2(t, 1f);
                    _hint.text = Loc.T("hud.recalling", HeroCombat.RecallDuration - Combat.RecallT);
                }
                else if (aiming)
                    _hint.text = Loc.T("hud.aim_ground");
                else if (!_dead && FoldMapBuilder.InFountain(Combat.transform.position, Player.Team))
                    _hint.text = Loc.T("hud.fountain");
                else if (!_dead)
                    _hint.text = Loc.T("hud.hint");
            }
        }

        void RefreshCountdown()
        {
            if (_countPanel == null || _countText == null)
                return;
            var rt = BattleRuntime.I;
            if (rt == null || rt.MatchOver)
            {
                _countPanel.SetActive(false);
                return;
            }
            if (rt.InPrep)
            {
                _countPanel.SetActive(true);
                _countText.fontSize = 88;
                _countText.text = Mathf.CeilToInt(rt.Countdown).ToString();
                return;
            }
            if (rt.MatchTime < 1.2f)
            {
                _countPanel.SetActive(true);
                _countText.fontSize = 64;
                _countText.text = Loc.T("hud.fight");
                return;
            }
            _countPanel.SetActive(false);
        }

        void RefreshSkill(SkillSlot slot, int index)
        {
            if (slot == null || slot.Btn == null || Combat == null)
                return;
            var keys = new[] { "Q", "W", "E" };
            var def = Combat.Ability(index);
            var name = def != null ? def.DisplayName : "";
            var prog = Combat.Progress;
            var rank = prog != null ? prog.RankOf(index) : 0;
            var max = HeroRules.MaxRank(index);
            var canUp = prog != null && prog.CanUpgrade(index);
            if (slot.Plus != null)
                slot.Plus.gameObject.SetActive(!_dead && canUp);

            if (_dead)
            {
                slot.Label.text = keys[index] + "\n—";
                slot.Btn.interactable = false;
                return;
            }

            if (rank < 1)
            {
                if (index == (int)AbilitySlot.C)
                    slot.Label.text = keys[index] + "\n" + Loc.T("hud.ult_locked", HeroRules.UltUnlockLevel[0]);
                else
                    slot.Label.text = keys[index] + "\n" + Loc.T("hud.locked");
                slot.Btn.interactable = canUp;
                return;
            }

            var cd = Combat.SkillCd[index];
            var rankMark = rank + "/" + max;
            if (cd > 0f)
            {
                slot.Label.text = keys[index] + "  " + cd.ToString("0.0") + "\n" + name + "  " + rankMark;
                slot.Btn.interactable = canUp;
            }
            else
            {
                slot.Label.text = keys[index] + "\n" + name + "  " + rankMark;
                slot.Btn.interactable = true;
            }
        }

        sealed class SkillSlot
        {
            public Button Btn;
            public Text Label;
            public Button Plus;
        }
    }
}
