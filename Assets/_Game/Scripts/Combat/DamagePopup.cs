using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    /// <summary>
    /// Floating combat text (FCT, «летающие цифры урона»).
    /// Локальный игрок видит только свой исходящий урон и весь входящий по себе.
    /// Объём цифр — широкая обводка, не FontStyle.Bold.
    /// </summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        const float Life = 0.92f;
        const float Stroke = 3.6f;
        const int PoolCap = 32;

        static readonly Vector2[] StrokeDir =
        {
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f),
            new Vector2(-1f, -0.5f), new Vector2(1f, -0.5f)
        };

        static readonly Queue<DamagePopup> Pool = new Queue<DamagePopup>(PoolCap);
        static Transform _root;
        static int _stagger;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Pool.Clear();
            _root = null;
            _stagger = 0;
        }

        public static void ClearWorld()
        {
            Pool.Clear();
            if (_root != null)
                Object.Destroy(_root.gameObject);
            _root = null;
        }

        Text _fill;
        Text[] _stroke;
        float _age;
        Vector3 _vel;
        Color _tint;

        public static void TryShow(CombatUnit target, CombatUnit source, float amount)
        {
            if (target == null || amount < 0.5f)
                return;
            var me = BattleRuntime.I != null ? BattleRuntime.I.Player : null;
            if (me == null)
                return;
            var incoming = target.IsPlayer;
            var outgoing = source != null && source.IsPlayer;
            if (!incoming && !outgoing)
                return;
            SpawnAt(target, amount, incoming);
        }

        static void SpawnAt(CombatUnit target, float amount, bool incoming)
        {
            var popup = Rent();
            var lift = target.IsStructure ? 2.45f : 1.9f;
            var n = _stagger++ % 5;
            var side = (n - 2) * 0.22f;
            popup.transform.position = target.transform.position + new Vector3(side, lift, 0.12f * (n % 2 == 0 ? 1f : -1f));
            popup.Begin(Mathf.RoundToInt(amount).ToString(), incoming);
        }

        static DamagePopup Rent()
        {
            while (Pool.Count > 0)
            {
                var p = Pool.Dequeue();
                if (p != null)
                {
                    p.gameObject.SetActive(true);
                    return p;
                }
            }
            return Create();
        }

        static Transform Root()
        {
            if (_root != null)
                return _root;
            var go = new GameObject("DamagePopups");
            _root = go.transform;
            return _root;
        }

        static DamagePopup Create()
        {
            var go = new GameObject("Dmg", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(Root(), false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 95;
            AppUi.DisableWorldRaycasts(go);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180f, 70f);
            go.transform.localScale = Vector3.one * 0.0125f;

            var popup = go.AddComponent<DamagePopup>();
            popup._stroke = new Text[StrokeDir.Length];
            var strokeCol = new Color(0.07f, 0.05f, 0.08f, 1f);
            for (var i = 0; i < StrokeDir.Length; i++)
            {
                popup._stroke[i] = MakeGlyph(rt, "S" + i, strokeCol);
                popup._stroke[i].rectTransform.anchoredPosition = StrokeDir[i] * Stroke;
            }
            popup._fill = MakeGlyph(rt, "Fill", Color.white);
            return popup;
        }

        static Text MakeGlyph(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = UiFactory.Font;
            t.fontSize = 42;
            t.fontStyle = FontStyle.Normal;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return t;
        }

        void Begin(string value, bool incoming)
        {
            _age = 0f;
            _tint = incoming ? GameTheme.DamageTaken : GameTheme.DamageDealt;
            _vel = new Vector3(Random.Range(-0.35f, 0.35f), 1.55f, 0f);
            transform.localScale = Vector3.one * 0.0125f * 1.18f;
            SetText(value);
            SetAlpha(1f);
        }

        void SetText(string value)
        {
            if (_fill != null)
                _fill.text = value;
            if (_stroke == null)
                return;
            for (var i = 0; i < _stroke.Length; i++)
            {
                if (_stroke[i] != null)
                    _stroke[i].text = value;
            }
        }

        void SetAlpha(float a)
        {
            if (_fill != null)
            {
                var c = _tint;
                c.a = a;
                _fill.color = c;
            }
            if (_stroke == null)
                return;
            var stroke = new Color(0.07f, 0.05f, 0.08f, a);
            for (var i = 0; i < _stroke.Length; i++)
            {
                if (_stroke[i] != null)
                    _stroke[i].color = stroke;
            }
        }

        void LateUpdate()
        {
            _age += Time.deltaTime;
            if (_age >= Life)
            {
                Recycle();
                return;
            }

            transform.position += _vel * Time.deltaTime;
            _vel.y = Mathf.Max(0.35f, _vel.y - 1.8f * Time.deltaTime);

            var u = Mathf.Clamp01(_age / Life);
            var punch = u < 0.12f ? Mathf.Lerp(1.18f, 1f, u / 0.12f) : 1f;
            transform.localScale = Vector3.one * 0.0125f * punch;
            SetAlpha(u > 0.58f ? 1f - (u - 0.58f) / 0.42f : 1f);

            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        void Recycle()
        {
            if (Pool.Count < PoolCap)
            {
                gameObject.SetActive(false);
                Pool.Enqueue(this);
            }
            else
                Destroy(gameObject);
        }
    }
}
