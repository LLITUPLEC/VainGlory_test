using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ashfold
{
    public sealed class MinimapView : MonoBehaviour, IPointerClickHandler
    {
        public static MinimapView I { get; private set; }

        RectTransform _root;
        readonly List<Image> _pool = new List<Image>(16);
        readonly List<Image> _used = new List<Image>(16);
        readonly List<PingDot> _pings = new List<PingDot>(4);

        struct PingDot
        {
            public Vector3 World;
            public float Until;
            public Image Img;
        }

        public static MinimapView Create(Transform hudRoot)
        {
            var frame = UiFactory.Box(hudRoot, new Vector2(0.02f, 0.55f), new Vector2(0.18f, 0.86f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Minimap");
            var map = UiFactory.Box(frame.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x1A2A28), "Map");
            map.raycastTarget = true;
            var lane = UiFactory.Box(map.transform, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.58f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x3A4A3C, 0.7f), "Lane");
            lane.raycastTarget = false;

            var view = map.gameObject.AddComponent<MinimapView>();
            view._root = map.rectTransform;
            return view;
        }

        void OnEnable()
        {
            I = this;
        }

        void OnDisable()
        {
            if (I == this)
                I = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || _root == null)
                return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, eventData.position, eventData.pressEventCamera, out local))
                return;
            var rect = _root.rect;
            var nx = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
            var ny = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            MapPingFx.TrySend(MapToWorld(new Vector2(nx, ny)));
        }

        public void AddPing(Vector3 world)
        {
            var go = new GameObject("PingDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_root, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = GameTheme.Gold;
            img.rectTransform.sizeDelta = new Vector2(16f, 16f);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = WorldToMap(world);
            img.rectTransform.anchoredPosition = Vector2.zero;
            _pings.Add(new PingDot { World = world, Until = Time.time + 2.8f, Img = img });
        }

        void Update()
        {
            var client = GameSession.I != null ? GameSession.I.MatchClient : null;
            if (client == null || !client.ConsumePings(out var pings))
                return;
            var me = GameSession.I.Nakama != null && GameSession.I.Nakama.Session != null
                ? GameSession.I.Nakama.Session.UserId
                : "";
            for (var i = 0; i < pings.Length; i++)
            {
                var p = pings[i];
                if (!string.IsNullOrEmpty(me) && p.userId == me)
                    continue;
                MapPingFx.Show(new Vector3(p.x, 1.35f, p.z));
            }
        }

        void LateUpdate()
        {
            _used.Clear();
            foreach (var u in CombatUnit.All)
            {
                if (u == null || !u.IsAlive)
                    continue;
                if (!u.IsHero && !u.IsStructure)
                    continue;

                Color color;
                float size;
                if (u.IsStructure)
                {
                    color = u.Team == TeamId.Dawn ? GameTheme.Teal : GameTheme.Crimson;
                    size = 10f;
                }
                else if (u.IsPlayer)
                {
                    color = GameTheme.Gold;
                    size = 12f;
                }
                else if (BattleRuntime.I != null && BattleRuntime.I.Player != null && u.Team == BattleRuntime.I.Player.Team)
                {
                    color = GameTheme.Teal;
                    size = 9f;
                }
                else
                {
                    // Враги в кусте не на миникарте.
                    var brush = BrushZone.FindAt(u.transform.position);
                    if (brush != null && BrushStealth.HiddenFromLocal(u))
                        continue;
                    color = GameTheme.Crimson;
                    size = 9f;
                }

                var img = NextDot();
                img.color = color;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchorMin = rt.anchorMax = WorldToMap(u.transform.position);
                rt.anchoredPosition = Vector2.zero;
                _used.Add(img);
            }

            for (var i = _pings.Count - 1; i >= 0; i--)
            {
                var ping = _pings[i];
                if (ping.Img == null || Time.time >= ping.Until)
                {
                    if (ping.Img != null)
                        Destroy(ping.Img.gameObject);
                    _pings.RemoveAt(i);
                    continue;
                }
                ping.Img.gameObject.SetActive(true);
                ping.Img.color = GameTheme.Gold;
                var pulse = 12f + 6f * Mathf.Sin(Time.time * 10f);
                ping.Img.rectTransform.sizeDelta = new Vector2(pulse, pulse);
                ping.Img.rectTransform.anchorMin = ping.Img.rectTransform.anchorMax = WorldToMap(ping.World);
                ping.Img.rectTransform.anchoredPosition = Vector2.zero;
            }

            for (var i = 0; i < _pool.Count; i++)
            {
                if (!_used.Contains(_pool[i]))
                    _pool[i].gameObject.SetActive(false);
            }
        }

        Image NextDot()
        {
            foreach (var img in _pool)
            {
                if (!_used.Contains(img) && !img.gameObject.activeSelf)
                {
                    img.gameObject.SetActive(true);
                    return img;
                }
            }
            var go = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(8f, 8f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            _pool.Add(image);
            return image;
        }

        static Vector2 WorldToMap(Vector3 world)
        {
            var x = Mathf.InverseLerp(-FoldMapBuilder.HalfLength - 4f, FoldMapBuilder.HalfLength + 4f, world.x);
            var y = Mathf.InverseLerp(-FoldMapBuilder.HalfWidth, FoldMapBuilder.HalfWidth, world.z);
            return new Vector2(x, y);
        }

        static Vector3 MapToWorld(Vector2 n)
        {
            var x = Mathf.Lerp(-FoldMapBuilder.HalfLength - 4f, FoldMapBuilder.HalfLength + 4f, n.x);
            var z = Mathf.Lerp(-FoldMapBuilder.HalfWidth, FoldMapBuilder.HalfWidth, n.y);
            return new Vector3(x, 1.35f, z);
        }
    }
}
