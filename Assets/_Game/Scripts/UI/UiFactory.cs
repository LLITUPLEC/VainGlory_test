using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Ashfold
{
    public static class UiFactory
    {
        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null)
                go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            Stretch(go.GetComponent<RectTransform>());
            return img;
        }

        public static Image Box(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color, string name = "Box")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal, bool wrap = false)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string caption, UnityAction onClick, Color bg, Color fg)
        {
            var go = new GameObject("Btn_" + caption, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bg, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(bg, Color.black, 0.2f);
            colors.disabledColor = new Color(bg.r, bg.g, bg.b, 0.35f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            Label(go.transform, caption, 28, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            return btn;
        }

        public static InputField Input(Transform parent, string placeholder)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = GameTheme.BgPanelSoft;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            Stretch(textRt, 16, 8);
            var text = textGo.GetComponent<Text>();
            text.font = Font;
            text.fontSize = 28;
            text.color = GameTheme.Text;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            phGo.transform.SetParent(go.transform, false);
            Stretch(phGo.GetComponent<RectTransform>(), 16, 8);
            var ph = phGo.GetComponent<Text>();
            ph.font = Font;
            ph.fontSize = 28;
            ph.fontStyle = FontStyle.Italic;
            ph.color = GameTheme.TextMuted;
            ph.alignment = TextAnchor.MiddleLeft;
            ph.text = placeholder;

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = ph;
            field.characterLimit = 16;
            return field;
        }

        public static void Stretch(RectTransform rt, float padX = 0, float padY = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        public static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
