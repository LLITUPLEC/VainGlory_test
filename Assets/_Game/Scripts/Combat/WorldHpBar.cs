using UnityEngine;

namespace Ashfold
{
    public sealed class WorldHpBar : MonoBehaviour
    {
        CombatUnit _unit;
        Transform _fill;

        public static WorldHpBar Attach(CombatUnit unit)
        {
            var root = new GameObject("HpBar").transform;
            root.SetParent(unit.transform, false);
            root.localPosition = new Vector3(0f, unit.IsStructure ? 2.6f : 1.35f, 0f);
            var bar = root.gameObject.AddComponent<WorldHpBar>();
            bar._unit = unit;

            var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bg.name = "Bg";
            bg.transform.SetParent(root, false);
            bg.transform.localScale = new Vector3(1.4f, 0.12f, 0.12f);
            Object.Destroy(bg.GetComponent<Collider>());
            bg.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(new Color(0.12f, 0.12f, 0.12f));

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Fill";
            fill.transform.SetParent(root, false);
            fill.transform.localScale = new Vector3(1.36f, 0.1f, 0.13f);
            Object.Destroy(fill.GetComponent<Collider>());
            fill.GetComponent<Renderer>().sharedMaterial = RuntimeMat.Make(unit.Team == TeamId.Dawn ? GameTheme.Teal : unit.Team == TeamId.Dusk ? GameTheme.Crimson : GameTheme.Gold);
            bar._fill = fill.transform;
            return bar;
        }

        void LateUpdate()
        {
            if (_unit == null)
                return;
            if (!_unit.IsAlive)
            {
                _fill.localScale = new Vector3(0.02f, 0.1f, 0.13f);
                return;
            }

            var t = _unit.Hp01;
            _fill.localScale = new Vector3(1.36f * t, 0.1f, 0.13f);
            _fill.localPosition = new Vector3(-0.68f * (1f - t), 0f, 0f);
            if (Camera.main != null)
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
