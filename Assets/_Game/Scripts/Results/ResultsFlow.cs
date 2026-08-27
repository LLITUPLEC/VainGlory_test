using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ashfold
{
    public sealed class ResultsFlow : MonoBehaviour
    {
        void Awake()
        {
            AppUi.EnsureEventSystem();
            if (Camera.main != null)
                Camera.main.backgroundColor = GameTheme.Bg;
        }

        void Start()
        {
            var result = GameSession.I != null ? GameSession.I.LastResult : null;
            if (result == null)
            {
                result = new MatchResult { Victory = false, EssenceReward = 0, ModeName = "Casual 3v3" };
                result.Rows.Add(new MatchStatRow { Name = "You", HeroId = "bastion", Team = 0, IsLocal = true });
            }

            Build(result);
        }

        void Build(MatchResult result)
        {
            var canvas = UiFactory.CreateCanvas("ResultsCanvas");
            var root = canvas.transform;
            UiFactory.Panel(root, GameTheme.Bg, "Bg");

            var banner = UiFactory.Box(root, new Vector2(0.15f, 0.82f), new Vector2(0.85f, 0.96f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Banner");
            var title = result.Surrendered ? "DEFEAT  ·  SURRENDER" : (result.Victory ? "VICTORY" : "DEFEAT");
            UiFactory.Label(banner.transform, title, 48, result.Victory ? GameTheme.Gold : GameTheme.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold);

            var sub = UiFactory.Box(root, new Vector2(0.2f, 0.76f), new Vector2(0.8f, 0.82f), Vector2.zero, Vector2.zero, Color.clear, "Sub");
            UiFactory.Label(sub.transform, result.ModeName + "  ·  " + result.MapName + "  ·  +" + result.EssenceReward + " ESSENCE", 18, GameTheme.Teal, TextAnchor.MiddleCenter);

            DrawTeam(root, result, 0, 0.04f, "DAWN");
            DrawTeam(root, result, 1, 0.52f, "DUSK");

            var cont = UiFactory.Button(root, "CONTINUE", OnContinue, GameTheme.Gold, GameTheme.Bg);
            UiFactory.SetAnchors(cont.GetComponent<RectTransform>(), new Vector2(0.35f, 0.04f), new Vector2(0.65f, 0.12f), Vector2.zero, Vector2.zero);
            cont.GetComponentInChildren<Text>().fontSize = 28;

            var stage = UiFactory.Box(root, new Vector2(0.02f, 0.01f), new Vector2(0.35f, 0.05f), Vector2.zero, Vector2.zero, Color.clear, "St");
            UiFactory.Label(stage.transform, "STAGE 4  ·  RESULTS", 14, GameTheme.GoldDim, TextAnchor.MiddleLeft);
        }

        void DrawTeam(Transform root, MatchResult result, int team, float x, string title)
        {
            var col = UiFactory.Box(root, new Vector2(x, 0.16f), new Vector2(x + 0.44f, 0.74f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Team" + team);
            var head = UiFactory.Box(col.transform, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "H");
            UiFactory.Label(head.transform, title, 22, team == 0 ? GameTheme.Teal : GameTheme.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold);

            var header = UiFactory.Box(col.transform, new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, "Hdr");
            UiFactory.Label(header.transform, "PLAYER            KDA      GOLD", 14, GameTheme.TextMuted, TextAnchor.MiddleLeft);
            UiFactory.Stretch(header.GetComponentInChildren<Text>().rectTransform, 10, 0);

            var i = 0;
            foreach (var row in result.Rows)
            {
                if (row.Team != team)
                    continue;
                var y = 0.62f - i * 0.18f;
                var line = UiFactory.Box(col.transform, new Vector2(0.04f, y), new Vector2(0.96f, y + 0.16f), Vector2.zero, Vector2.zero, row.IsLocal ? GameTheme.Hex(0xD4B45A, 0.18f) : GameTheme.BgPanelSoft, "R");
                var hero = GameContent.GetHero(row.HeroId);
                var items = "";
                foreach (var id in row.Items)
                {
                    var item = GameContent.GetItem(id);
                    if (item != null)
                        items += item.DisplayName[0] + " ";
                }
                if (string.IsNullOrEmpty(items))
                    items = "—";
                var label = (row.IsLocal ? "YOU · " : "") + row.Name + "  " + hero.DisplayName + "\n" +
                            row.Kills + " / " + row.Deaths + " / " + row.Assists + "     " + row.Gold + "g     " + items;
                UiFactory.Label(line.transform, label, 15, GameTheme.Text, TextAnchor.MiddleLeft, FontStyle.Normal, true);
                UiFactory.Stretch(line.GetComponentInChildren<Text>().rectTransform, 10, 4);
                i++;
            }
        }

        void OnContinue()
        {
            if (GameSession.I != null && GameSession.I.Profile != null && GameSession.I.LastResult != null)
                GameSession.I.Profile.Essence += GameSession.I.LastResult.EssenceReward;
            SceneManager.LoadScene(AppScenes.Hall);
        }
    }
}
