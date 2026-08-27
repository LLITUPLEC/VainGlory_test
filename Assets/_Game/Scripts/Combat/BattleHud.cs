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
        Text _skill;
        Text _items;
        Text _hint;
        Text _death;
        Text _clock;
        Button _skillBtn;
        Button _surrender;
        Image _recallBar;
        GameObject _deathPanel;
        GameObject _netPanel;
        Text _netText;
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
            var top = UiFactory.Box(root, new Vector2(0.02f, 0.88f), new Vector2(0.30f, 0.98f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Hero");
            _hp = UiFactory.Label(top.transform, "", 18, GameTheme.Text, TextAnchor.MiddleLeft);
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

            var bar = UiFactory.Box(root, new Vector2(0.22f, 0.03f), new Vector2(0.50f, 0.14f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Hint");
            _hint = UiFactory.Label(bar.transform, Loc.T("hud.hint"), 15, GameTheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, true);

            _skillBtn = UiFactory.Button(root, "Q", OnSkill, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(_skillBtn.GetComponent<RectTransform>(), new Vector2(0.52f, 0.03f), new Vector2(0.64f, 0.14f), Vector2.zero, Vector2.zero);
            _skill = _skillBtn.GetComponentInChildren<Text>();
            _skill.fontSize = 18;

            var shop = UiFactory.Button(root, Loc.T("hud.shop"), OnShop, GameTheme.BgPanelSoft, GameTheme.Gold);
            UiFactory.SetAnchors(shop.GetComponent<RectTransform>(), new Vector2(0.66f, 0.03f), new Vector2(0.78f, 0.14f), Vector2.zero, Vector2.zero);
            shop.GetComponentInChildren<Text>().fontSize = 16;

            var recall = UiFactory.Button(root, Loc.T("hud.recall"), OnRecall, GameTheme.BgPanelSoft, GameTheme.Teal);
            UiFactory.SetAnchors(recall.GetComponent<RectTransform>(), new Vector2(0.80f, 0.03f), new Vector2(0.92f, 0.14f), Vector2.zero, Vector2.zero);
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

            var stage = UiFactory.Box(root, new Vector2(0.32f, 0.94f), new Vector2(0.68f, 0.99f), Vector2.zero, Vector2.zero, Color.clear, "St");
            UiFactory.Label(stage.transform, Loc.T("hud.stage"), 14, GameTheme.GoldDim, TextAnchor.MiddleCenter);
        }

        void OnSkill()
        {
            if (!_dead && Combat != null)
                Combat.TryCastSkill();
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
            if (_dead)
                _hp.text = def.DisplayName.ToUpperInvariant() + "\n" + Loc.T("hud.dead");
            else
                _hp.text = def.DisplayName.ToUpperInvariant() + "\n" + Mathf.CeilToInt(Player.Hp) + " / " + Mathf.CeilToInt(Player.MaxHp);

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

            var skillName = GameContent.HeroSkill(def);
            if (Combat.SkillCd > 0f || _dead)
            {
                _skill.text = _dead ? "Q\n—" : "Q  " + Combat.SkillCd.ToString("0.0") + "\n" + skillName;
                _skillBtn.interactable = false;
            }
            else
            {
                _skill.text = "Q\n" + skillName;
                _skillBtn.interactable = true;
            }

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
                else if (!_dead && FoldMapBuilder.InFountain(Combat.transform.position, Player.Team))
                    _hint.text = Loc.T("hud.fountain");
            }
        }
    }
}
