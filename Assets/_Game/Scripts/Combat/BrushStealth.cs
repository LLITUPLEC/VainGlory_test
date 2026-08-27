using UnityEngine;

namespace Ashfold
{
    /// <summary>Куст: враги внутри невидимы, пока союзник не зашёл в тот же куст.</summary>
    public sealed class BrushZone : MonoBehaviour
    {
        public static BrushZone[] All = System.Array.Empty<BrushZone>();

        public float Radius = 3.2f;

        void OnEnable()
        {
            var list = new System.Collections.Generic.List<BrushZone>(All) { this };
            All = list.ToArray();
        }

        void OnDisable()
        {
            var list = new System.Collections.Generic.List<BrushZone>(All);
            list.Remove(this);
            All = list.ToArray();
        }

        public bool Contains(Vector3 world)
        {
            world.y = 0f;
            var p = transform.position;
            p.y = 0f;
            return (world - p).sqrMagnitude <= Radius * Radius;
        }

        public static BrushZone FindAt(Vector3 world)
        {
            foreach (var b in All)
            {
                if (b != null && b.Contains(world))
                    return b;
            }
            return null;
        }
    }

    public sealed class BrushStealth : MonoBehaviour
    {
        public CombatUnit Unit;
        Renderer[] _renderers;
        WorldHpBar _bar;
        bool _hidden;

        void Start()
        {
            if (Unit == null)
                Unit = GetComponent<CombatUnit>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _bar = GetComponentInChildren<WorldHpBar>(true);
        }

        void LateUpdate()
        {
            if (Unit == null || !Unit.IsAlive)
                return; // смерть/респаун сами управляют рендерами

            var player = BattleRuntime.I != null ? BattleRuntime.I.Player : null;
            if (player == null || Unit.IsPlayer || Unit.Team == player.Team)
            {
                SetHidden(false);
                return;
            }

            var myBrush = BrushZone.FindAt(transform.position);
            if (myBrush == null)
            {
                SetHidden(false);
                return;
            }

            var playerBrush = BrushZone.FindAt(player.transform.position);
            var revealed = playerBrush == myBrush;
            SetHidden(!revealed);
        }

        void SetHidden(bool hidden)
        {
            if (_hidden == hidden)
                return;
            _hidden = hidden;
            if (_renderers != null)
            {
                foreach (var r in _renderers)
                {
                    if (r != null)
                        r.enabled = !hidden;
                }
            }
            if (_bar != null)
                _bar.gameObject.SetActive(!hidden);
        }
    }
}
