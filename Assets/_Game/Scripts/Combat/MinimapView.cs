using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ashfold
{
    public sealed class MinimapView : MonoBehaviour
    {
        RectTransform _root;
        readonly List<Image> _pool = new List<Image>(16);
        readonly List<Image> _used = new List<Image>(16);

        public static MinimapView Create(Transform hudRoot)
        {
            var frame = UiFactory.Box(hudRoot, new Vector2(0.02f, 0.55f), new Vector2(0.18f, 0.86f), Vector2.zero, Vector2.zero, GameTheme.BgPanel, "Minimap");
            var map = UiFactory.Box(frame.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x1A2A28), "Map");
            // Lane stripe
            UiFactory.Box(map.transform, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.58f), Vector2.zero, Vector2.zero, GameTheme.Hex(0x3A4A3C, 0.7f), "Lane");

            var view = map.gameObject.AddComponent<MinimapView>();
            view._root = map.rectTransform;
            return view;
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
                    if (brush != null)
                    {
                        var player = BattleRuntime.I != null ? BattleRuntime.I.Player : null;
                        if (player == null || BrushZone.FindAt(player.transform.position) != brush)
                            continue;
                    }
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
    }
}
