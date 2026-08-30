using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>Каталог Hall: герои (2.2) и магазин (2.3).</summary>
    public static class HallCatalog
    {
        public static void OpenHeroes(System.Action onChanged)
        {
            var canvas = AppUi.OverlayCanvas("HeroesOverlay");
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.78f), "Dim");

            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            var titleBox = UiFactory.Box(sheet.transform, new Vector2(0.04f, 0.88f), new Vector2(0.78f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "Title");
            UiFactory.Label(titleBox.transform, Loc.T("catalog.heroes"), 32, GameTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);

            var close = UiFactory.Button(sheet.transform, Loc.T("catalog.close"), () => Object.Destroy(canvas.gameObject), GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(close.GetComponent<RectTransform>(), new Vector2(0.82f, 0.88f), new Vector2(0.98f, 0.98f), new Vector2(8, 8), new Vector2(-8, -8));
            close.GetComponentInChildren<Text>().fontSize = 18;

            for (var i = 0; i < GameContent.Heroes.Length; i++)
            {
                var hero = GameContent.Heroes[i];
                var unlocked = GameSession.I == null || GameSession.I.Profile == null || GameSession.I.Profile.IsHeroUnlocked(hero.Id);
                var x0 = 0.04f + i * 0.32f;
                var card = UiFactory.Button(sheet.transform, "", () =>
                {
                    if (!unlocked)
                        return;
                    GameSession.I.ShowcaseHeroId = hero.Id;
                    onChanged?.Invoke();
                    Object.Destroy(canvas.gameObject);
                }, GameTheme.BgPanelSoft, GameTheme.Text);
                UiFactory.SetAnchors(card.GetComponent<RectTransform>(), new Vector2(x0, 0.12f), new Vector2(x0 + 0.30f, 0.82f), Vector2.zero, Vector2.zero);
                Object.Destroy(card.GetComponentInChildren<Text>().gameObject);

                var tint = unlocked ? GameContent.HeroColor(hero.Id) : GameTheme.TextMuted;
                var color = UiFactory.Box(card.transform, new Vector2(0.2f, 0.52f), new Vector2(0.8f, 0.90f), Vector2.zero, Vector2.zero, tint, "Swatch");
                color.raycastTarget = false;
                var caption = unlocked
                    ? hero.DisplayName.ToUpperInvariant() + "\n" + GameContent.RoleLabel(hero.Role) + "\n" + GameContent.HeroTagline(hero)
                      + "\nQ " + GameContent.HeroAbility(hero, 0)
                      + "  W " + GameContent.HeroAbility(hero, 1)
                      + "  E " + GameContent.HeroAbility(hero, 2)
                    : hero.DisplayName.ToUpperInvariant() + "\n" + Loc.T("catalog.locked");
                UiFactory.Label(card.transform, caption, 16, unlocked ? GameTheme.Text : GameTheme.TextMuted, TextAnchor.LowerCenter, FontStyle.Bold, true);
            }
        }

        public static void OpenShop()
        {
            var canvas = AppUi.OverlayCanvas("ShopOverlay");
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.78f), "Dim");
            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            var titleBox = UiFactory.Box(sheet.transform, new Vector2(0.04f, 0.88f), new Vector2(0.78f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "Title");
            UiFactory.Label(titleBox.transform, Loc.T("catalog.shop"), 26, GameTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);

            var close = UiFactory.Button(sheet.transform, Loc.T("catalog.close"), () => Object.Destroy(canvas.gameObject), GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(close.GetComponent<RectTransform>(), new Vector2(0.82f, 0.88f), new Vector2(0.98f, 0.98f), new Vector2(8, 8), new Vector2(-8, -8));
            close.GetComponentInChildren<Text>().fontSize = 18;

            for (var i = 0; i < GameContent.Items.Length; i++)
            {
                var item = GameContent.Items[i];
                var col = i % 3;
                var row = i / 3;
                var x0 = 0.04f + col * 0.32f;
                var y0 = 0.48f - row * 0.36f;
                var card = UiFactory.Box(sheet.transform, new Vector2(x0, y0), new Vector2(x0 + 0.30f, y0 + 0.32f), Vector2.zero, Vector2.zero, GameTheme.BgPanelSoft, item.Id);
                UiFactory.Label(card.transform,
                    GameContent.ItemName(item).ToUpperInvariant() + "\n" + GameContent.ItemBranch(item) + " · " + item.Cost + "g\n" + GameContent.ItemEffect(item) + "\n" + Loc.T("catalog.in_match"),
                    16, GameTheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal, true);
            }
        }
    }
}
