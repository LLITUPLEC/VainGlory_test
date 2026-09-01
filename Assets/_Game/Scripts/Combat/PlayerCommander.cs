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

        public void OnWorldPointer(Vector2 screen)
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
            if (_aimMark == null)
                return;
            if (destroyMark)
            {
                Destroy(_aimMark);
                _aimMark = null;
                return;
            }
            _aimMark.SetActive(false);
        }

        void LateUpdate()
        {
            if (_aimSlot < 0 || _aimMark == null)
                return;
            var cam = Camera.main;
            if (cam == null)
                return;
            var ray = cam.ScreenPointToRay(PointerScreen());
            if (!PlanePoint(ray, out var p))
                return;
            _aimMark.SetActive(true);
            _aimMark.transform.position = p + Vector3.up * 0.08f;
            var def = Hero != null ? Hero.Ability(_aimSlot) : null;
            var rank = Hero != null && Hero.Progress != null ? Hero.Progress.RankOf(_aimSlot) : 1;
            var r = def != null ? Mathf.Max(1.6f, def.Dur(Mathf.Max(1, rank))) : 2f;
            _aimMark.transform.localScale = new Vector3(r * 2f, 0.12f, r * 2f);
        }

        void EnsureMark()
        {
            if (_aimMark != null)
            {
                _aimMark.SetActive(true);
                return;
            }
            _aimMark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var col = _aimMark.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
            _aimMark.name = "AimMark";
            _aimMark.layer = 2;
            _aimMark.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(new Color(GameTheme.Gold.r, GameTheme.Gold.g, GameTheme.Gold.b, 0.35f));
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
                if (u == null || !u.IsAlive)
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
