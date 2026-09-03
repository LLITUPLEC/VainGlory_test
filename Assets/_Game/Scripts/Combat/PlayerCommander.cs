using UnityEngine;
using UnityEngine.InputSystem;

namespace Ashfold
{
    public sealed class PlayerCommander : MonoBehaviour
    {
        public HeroCombat Hero;
        public CombatUnit Unit;
        int _aimSlot = -1;
        GameObject _aimMark;
        GameObject _aimRange;
        float _aimOpenedAt = -10f;

        void Update()
        {
            if (Hero == null || Unit == null || !Unit.IsAlive)
            {
                StopAim();
                return;
            }
            Hero.EnsureControlIfAlive();
            if (BattleRuntime.I != null && BattleRuntime.I.InPrep)
                return;
            if (PressedKey(Key.Escape) || RightClicked())
                StopAim();
            if (PressedKey(Key.Q) || PressedKey(Key.A))
                PressSkill(0);
            if (PressedKey(Key.W) || PressedKey(Key.S))
                PressSkill(1);
            if (PressedKey(Key.E) || PressedKey(Key.C))
                PressSkill(2);
            if (PressedKey(Key.B))
            {
                StopAim();
                FountainShop.Open(Hero);
            }
            if (PressedKey(Key.R))
            {
                StopAim();
                Hero.TryRecall();
            }
        }

        public void OnWorldPointer(Vector2 screen, bool fatFinger = false)
        {
            if (Hero == null || Unit == null || !Unit.IsAlive)
                return;
            if (BattleRuntime.I != null && BattleRuntime.I.InPrep)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;
            var ray = cam.ScreenPointToRay(screen);
            if (PingHeld() && PlanePoint(ray, out var pingAt))
            {
                MapPingFx.TrySend(pingAt);
                return;
            }
            if (_aimSlot >= 0)
            {
                if (PlanePoint(ray, out var aimAt))
                    Hero.TryCastSkill(_aimSlot, Hero.AttackTarget, aimAt);
                StopAim();
                return;
            }
            var unit = PickEnemy(ray, screen, cam, fatFinger);
            if (unit != null)
            {
                Hero.CommandAttack(unit);
                if (Net())
                    GameSession.I.MatchClient.SendAttack(unit.NetId);
                return;
            }

            if (PlanePoint(ray, out var point))
            {
                Hero.CommandMove(point);
                if (Net())
                    GameSession.I.MatchClient.SendMove(point.x, point.z);
            }
        }

        static bool Net()
        {
            return GameSession.I != null
                   && GameSession.I.Match != null
                   && GameSession.I.Match.IsNetworked
                   && GameSession.I.MatchClient != null;
        }

        public int AimSlot => _aimSlot;

        /// <summary>Как в VG: невыученное умение с очком — сначала прокачка, иначе каст / прицел.</summary>
        public void PressSkill(int slot)
        {
            if (Hero == null)
                return;
            var p = Hero.Progress;
            if (Shift() || (p != null && p.RankOf(slot) < 1 && p.CanUpgrade(slot)))
            {
                StopAim();
                Hero.TryUpgrade(slot);
                return;
            }
            RequestSkill(slot);
        }

        public void RequestSkill(int slot)
        {
            if (Hero == null)
                return;
            var def = Hero.Ability(slot);
            if (def == null)
                return;
            if (_aimSlot == slot)
            {
                if (Time.unscaledTime - _aimOpenedAt < 0.2f)
                    return;
                StopAim();
                return;
            }
            if (_aimSlot >= 0)
                StopAim();
            if (def.Targeting == AbilityTargeting.Ground)
            {
                if (!Hero.SlotReady(slot))
                    return;
                _aimSlot = slot;
                _aimOpenedAt = Time.unscaledTime;
                EnsureMark();
                return;
            }
            Hero.TryCastSkill(slot, Hero.AttackTarget, Hero.transform.position);
        }

        void StopAim()
        {
            ClearAim(false);
        }

        void ClearAim(bool destroyMark)
        {
            _aimSlot = -1;
            if (_aimMark != null)
            {
                if (destroyMark)
                {
                    Destroy(_aimMark);
                    _aimMark = null;
                }
                else
                    _aimMark.SetActive(false);
            }
            if (_aimRange != null)
            {
                if (destroyMark)
                {
                    Destroy(_aimRange);
                    _aimRange = null;
                }
                else
                    _aimRange.SetActive(false);
            }
        }

        void LateUpdate()
        {
            if (_aimSlot < 0)
                return;
            var cam = Camera.main;
            if (cam == null || Hero == null)
                return;
            var def = Hero.Ability(_aimSlot);
            var rank = Hero.Progress != null ? Hero.Progress.RankOf(_aimSlot) : 1;
            rank = Mathf.Max(1, rank);

            if (_aimRange != null)
            {
                _aimRange.SetActive(true);
                var hp = Hero.transform.position;
                var y = GroundProbe.SurfaceY(hp) + 0.25f;
                _aimRange.transform.position = new Vector3(hp.x, y, hp.z);
                var castR = def != null ? Mathf.Max(2f, def.Rng(rank)) : 8f;
                _aimRange.transform.localScale = Vector3.one * castR;
            }

            if (_aimMark == null)
                return;
            var ray = cam.ScreenPointToRay(PointerScreen());
            if (!PlanePoint(ray, out var at))
                return;
            _aimMark.SetActive(true);
            var markY = GroundProbe.SurfaceY(at) + 0.2f;
            _aimMark.transform.position = new Vector3(at.x, markY, at.z);
            var r = def != null ? Mathf.Max(1.6f, def.Dur(rank)) : 2f;
            _aimMark.transform.localScale = Vector3.one * r;
        }

        void EnsureMark()
        {
            if (_aimMark == null)
                _aimMark = MakeRangeCircle("AimMark", new Color(GameTheme.Gold.r, GameTheme.Gold.g, GameTheme.Gold.b, 0.95f), 0.22f);
            else
                _aimMark.SetActive(true);

            if (_aimRange == null)
                _aimRange = MakeRangeCircle("AimRange", new Color(GameTheme.Teal.r, GameTheme.Teal.g, GameTheme.Teal.b, 0.95f), 0.28f);
            else
                _aimRange.SetActive(true);
        }

        static GameObject MakeRangeCircle(string name, Color color, float width)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            const int segments = 72;
            lr.positionCount = segments;
            for (var i = 0; i < segments; i++)
            {
                var a = (i / (float)segments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            lr.sharedMaterial = RuntimeMat.Make(color);
            lr.startColor = color;
            lr.endColor = color;
            return go;
        }

        void OnDisable()
        {
            ClearAim(true);
        }

        void OnDestroy()
        {
            ClearAim(true);
        }

        static bool Shift()
        {
            if (Keyboard.current == null)
                return false;
            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }

        static bool PingHeld()
        {
            if (Keyboard.current == null)
                return false;
            return Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed
                   || Keyboard.current.gKey.isPressed;
        }

        static bool RightClicked()
        {
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        }

        static bool PressedKey(UnityEngine.InputSystem.Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }

        static Vector2 PointerScreen()
        {
            if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.isPressed))
                return Mouse.current.position.ReadValue();
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            if (Touchscreen.current != null)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            return Vector2.zero;
        }

        static bool PlanePoint(Ray ray, out Vector3 point)
        {
            var hits = Physics.RaycastAll(ray, 250f);
            var bestY = float.NegativeInfinity;
            var found = false;
            point = default;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || !GroundProbe.IsGround(hit.collider))
                    continue;
                if (hit.point.y < bestY)
                    continue;
                bestY = hit.point.y;
                point = hit.point;
                found = true;
            }
            if (found)
                return true;
            var plane = new Plane(Vector3.up, new Vector3(0f, GroundProbe.DefaultSurfaceY, 0f));
            if (!plane.Raycast(ray, out var dist))
                return false;
            point = ray.GetPoint(dist);
            return true;
        }

        CombatUnit PickEnemy(Ray ray, Vector2 screen, Camera cam, bool fatFinger)
        {
            var pad = fatFinger ? 108f : 64f;
            CombatUnit best = null;
            var bestScore = float.MaxValue;
            foreach (var u in CombatUnit.All)
            {
                if (u == null || !u.IsAlive || !Unit.IsEnemy(u))
                    continue;
                if (!TapHitsUnit(u, cam, screen, pad, out var score))
                    continue;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = u;
                }
            }
            if (best != null)
                return best;
            if (Physics.SphereCast(ray, fatFinger ? 1.15f : 0.85f, out var hit, 250f, ~0, QueryTriggerInteraction.Ignore))
            {
                var u = hit.collider.GetComponentInParent<CombatUnit>();
                if (u != null && u.IsAlive && Unit.IsEnemy(u))
                    return u;
            }
            var rayUnit = PickUnit(ray);
            if (rayUnit != null && rayUnit.IsAlive && Unit.IsEnemy(rayUnit))
                return rayUnit;
            return null;
        }

        static bool TapHitsUnit(CombatUnit u, Camera cam, Vector2 screen, float pad, out float score)
        {
            score = float.MaxValue;
            if (!ScreenRectOf(u, cam, out var rect, out var center))
                return false;
            rect.xMin -= pad;
            rect.yMin -= pad;
            rect.xMax += pad;
            rect.yMax += pad;
            if (!rect.Contains(screen))
                return false;
            score = Vector2.Distance(screen, center);
            return true;
        }

        static bool ScreenRectOf(CombatUnit u, Camera cam, out Rect rect, out Vector2 center)
        {
            rect = default;
            center = default;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var any = false;
            var rends = u.GetComponentsInChildren<Renderer>();
            for (var i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || !r.enabled)
                    continue;
                EncapsulateScreen(cam, r.bounds, ref min, ref max, ref any);
            }
            if (!any)
            {
                var col = u.GetComponent<Collider>();
                if (col != null)
                    EncapsulateScreen(cam, col.bounds, ref min, ref max, ref any);
            }
            if (!any)
            {
                var sp = cam.WorldToScreenPoint(u.transform.position + Vector3.up * 1.1f);
                if (sp.z <= 0.05f)
                    return false;
                center = new Vector2(sp.x, sp.y);
                rect = new Rect(center.x - 40f, center.y - 40f, 80f, 80f);
                return true;
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            center = rect.center;
            return true;
        }

        static void EncapsulateScreen(Camera cam, Bounds b, ref Vector2 min, ref Vector2 max, ref bool any)
        {
            var c = b.center;
            var e = b.extents;
            for (var i = 0; i < 8; i++)
            {
                var p = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                var sp = cam.WorldToScreenPoint(p);
                if (sp.z <= 0.05f)
                    continue;
                any = true;
                if (sp.x < min.x) min.x = sp.x;
                if (sp.y < min.y) min.y = sp.y;
                if (sp.x > max.x) max.x = sp.x;
                if (sp.y > max.y) max.y = sp.y;
            }
        }

        static CombatUnit PickUnit(Ray ray)
        {
            var hits = Physics.RaycastAll(ray, 250f);
            CombatUnit best = null;
            var bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                var u = hit.collider.GetComponentInParent<CombatUnit>();
                if (u == null || !u.IsAlive)
                    continue;
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    best = u;
                }
            }
            return best;
        }
    }
}
