using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ashfold
{
    public sealed class PlayerCommander : MonoBehaviour
    {
        public HeroCombat Hero;
        public CombatUnit Unit;

        void Update()
        {
            if (Hero == null || Unit == null || !Unit.IsAlive)
                return;
            Hero.EnsureControlIfAlive();
            if (Hero.Motor != null && Hero.Motor.Locked)
                return;
            if (QPressed())
                Hero.TryCastSkill();
            if (PressedKey(UnityEngine.InputSystem.Key.B))
                FountainShop.Open(Hero);
            if (PressedKey(UnityEngine.InputSystem.Key.R))
                Hero.TryRecall();
            if (!Pressed())
                return;
            if (OverUi())
                return;

            var cam = Camera.main;
            if (cam == null)
                return;
            var screen = PointerScreen();
            var ray = cam.ScreenPointToRay(screen);
            if (PingHeld() && PlanePoint(ray, out var pingAt))
            {
                MapPingFx.TrySend(pingAt);
                return;
            }
            var unit = PickUnit(ray);
            if (unit == null && PlanePoint(ray, out var ground))
                unit = NearestUnit(ground, 1.5f);

            if (unit != null && Unit.IsEnemy(unit))
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

        static bool QPressed()
        {
            return PressedKey(UnityEngine.InputSystem.Key.Q);
        }

        static bool PingHeld()
        {
            if (Keyboard.current == null)
                return false;
            return Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed
                   || Keyboard.current.gKey.isPressed;
        }

        static bool PressedKey(UnityEngine.InputSystem.Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }

        static bool Pressed()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        }

        static Vector2 PointerScreen()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            if (Touchscreen.current != null)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            return Vector2.zero;
        }

        static readonly System.Collections.Generic.List<RaycastResult> UiHits = new System.Collections.Generic.List<RaycastResult>(8);

        static bool OverUi()
        {
            var es = EventSystem.current;
            if (es == null)
                return false;
            var ped = new PointerEventData(es) { position = PointerScreen() };
            UiHits.Clear();
            es.RaycastAll(ped, UiHits);
            return UiHits.Count > 0;
        }

        static bool PlanePoint(Ray ray, out Vector3 point)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var dist))
            {
                point = default;
                return false;
            }
            point = ray.GetPoint(dist);
            return true;
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

        static CombatUnit NearestUnit(Vector3 point, float radius)
        {
            CombatUnit best = null;
            var bestSq = radius * radius;
            foreach (var u in CombatUnit.All)
            {
                if (!u.IsAlive)
                    continue;
                var d = u.transform.position;
                d.y = point.y;
                var sq = (d - point).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = u;
                }
            }
            return best;
        }
    }
}
