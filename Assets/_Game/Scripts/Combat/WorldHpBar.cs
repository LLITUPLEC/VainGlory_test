using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>World-space UI, без z-fighting кубов (на телефоне заливка пропадала).</summary>
    public sealed class WorldHpBar : MonoBehaviour
    {
        static Sprite _white;

        CombatUnit _unit;
        Image _fill;

        public static WorldHpBar Attach(CombatUnit unit)
        {
            var go = new GameObject("HpBar");
            go.transform.SetParent(unit.transform, false);
            go.transform.localPosition = new Vector3(0f, unit.IsStructure ? 2.6f : 1.55f, 0f);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 80;
            AppUi.DisableWorldRaycasts(go);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1.6f, 0.18f);
            rt.localScale = Vector3.one;

            var bar = go.AddComponent<WorldHpBar>();
            bar._unit = unit;

            var sprite = WhiteSprite();
            MakeImage(rt, "Bg", sprite, new Color(0.08f, 0.08f, 0.09f, 0.92f), Vector2.zero, Vector2.one);

            var fillImg = MakeImage(rt, "Fill", sprite, FillColor(unit), Vector2.zero, Vector2.one);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            bar._fill = fillImg;
            return bar;
        }

        void LateUpdate()
        {
            if (_unit == null || _fill == null)
                return;

            var parent = transform.parent;
            if (parent != null)
            {
                var ls = parent.lossyScale;
                if (ls.x > 0.01f && ls.y > 0.01f && ls.z > 0.01f)
                    transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            }

            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            _fill.fillAmount = _unit.IsAlive ? _unit.Hp01 : 0f;
            _fill.color = FillColor(_unit);
        }

        static Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Color FillColor(CombatUnit unit)
        {
            var t = unit != null && unit.IsAlive ? unit.Hp01 : 0f;
            if (AllyToLocal(unit))
                return Color.Lerp(GameTheme.AllyHpLow, GameTheme.AllyHp, t);
            return Color.Lerp(GameTheme.EnemyHpLow, GameTheme.EnemyHp, t);
        }

        public static bool AllyToLocal(CombatUnit unit)
        {
            if (unit == null || unit.Team == TeamId.Neutral)
                return false;
            var me = BattleRuntime.I != null ? BattleRuntime.I.Player : null;
            if (me == null)
                return unit.Team == TeamId.Dawn;
            return unit.Team == me.Team;
        }

        static Sprite WhiteSprite()
        {
            if (_white != null)
                return _white;
            var tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _white.name = "AshfoldWhite";
            return _white;
        }
    }
}
