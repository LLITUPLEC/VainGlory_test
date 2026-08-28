using UnityEngine;

namespace Ashfold
{
    /// <summary>Скрывает врага в кусте, пока союзник не зашёл в тот же куст.
    /// После боя с героем невидимость включается только спустя 2 с без геройского урона.</summary>
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

            SetHidden(HiddenFromLocal(Unit));
        }

        public static bool HiddenFromLocal(CombatUnit unit)
        {
            if (unit == null || !unit.IsAlive)
                return false;
            var player = BattleRuntime.I != null ? BattleRuntime.I.Player : null;
            if (player == null || unit.IsPlayer || unit.Team == player.Team)
                return false;

            var myBrush = BrushZone.FindAt(unit.transform.position);
            if (myBrush == null)
                return false;
            if (BrushZone.FindAt(player.transform.position) == myBrush)
                return false;
            if (unit.InHeroCombat)
                return false;
            return true;
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
