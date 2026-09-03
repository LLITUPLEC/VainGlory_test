using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    public static class FountainShop
    {
        public static void Open(HeroCombat hero)
        {
            if (hero == null || hero.Unit == null)
                return;
            if (!FoldMapBuilder.InFountain(hero.transform.position, hero.Unit.Team))
                return;

            var canvas = AppUi.OverlayCanvas("ShopBattle", 35);
            UiFactory.Panel(canvas.transform, GameTheme.Hex(0x000000, 0.72f), "Dim");
            var sheet = UiFactory.Box(canvas.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Sheet");
            var title = UiFactory.Box(sheet.transform, new Vector2(0.04f, 0.90f), new Vector2(0.70f, 0.98f), Vector2.zero, Vector2.zero, Color.clear, "T");
            var titleLabel = UiFactory.Label(title.transform, Loc.T("shop.fountain", BattleRuntime.I != null ? BattleRuntime.I.Gold : 0), 28, GameTheme.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.EnableBestFit(titleLabel, 18, 32);

            var close = UiFactory.Button(sheet.transform, Loc.T("shop.close"), () => Object.Destroy(canvas.gameObject), GameTheme.BgPanelSoft, GameTheme.Text);
            UiFactory.SetAnchors(close.GetComponent<RectTransform>(), new Vector2(0.82f, 0.90f), new Vector2(0.98f, 0.98f), new Vector2(8, 8), new Vector2(-8, -8));
            close.GetComponentInChildren<Text>().fontSize = 20;

            for (var i = 0; i < GameContent.Items.Length; i++)
            {
                var item = GameContent.Items[i];
                var col = i % 3;
                var row = i / 3;
                var x0 = 0.04f + col * 0.32f;
                var y0 = 0.62f - row * 0.28f;
                var owned = 0;
                if (item.BuffOnly)
                    owned = hero.Heroism ? 1 : 0;
                else
                {
                    foreach (var id in hero.Items)
                    {
                        if (id == item.Id)
                            owned++;
                    }
                }

                var card = UiFactory.Button(sheet.transform, "", () =>
                {
                    if (hero.TryBuy(item))
                    {
                        Object.Destroy(canvas.gameObject);
                        Open(hero);
                    }
                }, item.BuffOnly ? GameTheme.Gold : GameTheme.BgPanelSoft, GameTheme.Text);
                UiFactory.SetAnchors(card.GetComponent<RectTransform>(), new Vector2(x0, y0), new Vector2(x0 + 0.30f, y0 + 0.26f), Vector2.zero, Vector2.zero);
                Object.Destroy(card.GetComponentInChildren<Text>().gameObject);
                var ownedMark = item.BuffOnly
                    ? (owned > 0 ? "\n" + Loc.T("shop.active") : "")
                    : (owned > 0 ? "\nx" + owned : "");
                var label = UiFactory.Label(card.transform,
                    GameContent.ItemName(item).ToUpperInvariant() + "\n" + GameContent.ItemBranch(item) + " · " + item.Cost + "g\n" + GameContent.ItemEffect(item) + ownedMark,
                    22, item.BuffOnly ? GameTheme.Bg : GameTheme.Text, TextAnchor.MiddleCenter, FontStyle.Normal, true);
                UiFactory.EnableBestFit(label, 14, 24);
                if (item.BuffOnly && hero.Heroism)
                    card.interactable = false;
            }
        }
    }
}
