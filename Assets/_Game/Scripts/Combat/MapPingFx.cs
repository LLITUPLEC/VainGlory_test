using UnityEngine;

namespace Ashfold
{
    /// <summary>Пинг по карте (этап 6.3): локальный маркер + рассылка союзникам.</summary>
    public sealed class MapPingFx : MonoBehaviour
    {
        const float Life = 2.8f;
        const float Cooldown = 2f;
        static float _nextSend;

        float _born;
        Transform _beam;

        public static void TrySend(Vector3 world)
        {
            if (Time.unscaledTime < _nextSend)
                return;
            _nextSend = Time.unscaledTime + Cooldown;
            world.y = 1.35f;
            Show(world);
            if (GameSession.I != null
                && GameSession.I.Match != null
                && GameSession.I.Match.IsNetworked
                && GameSession.I.MatchClient != null)
                GameSession.I.MatchClient.SendMapPing(world.x, world.z);
        }

        public static void Show(Vector3 world)
        {
            var go = new GameObject("MapPing");
            go.transform.position = world;
            var fx = go.AddComponent<MapPingFx>();
            fx._born = Time.time;
            fx._beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            fx._beam.SetParent(go.transform, false);
            fx._beam.localPosition = new Vector3(0f, 2.2f, 0f);
            fx._beam.localScale = new Vector3(0.35f, 2.2f, 0.35f);
            var col = fx._beam.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);
            var rend = fx._beam.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = GameTheme.Gold;
            if (MinimapView.I != null)
                MinimapView.I.AddPing(world);
        }

        void Update()
        {
            var t = Time.time - _born;
            if (t >= Life)
            {
                Destroy(gameObject);
                return;
            }
            var pulse = 0.7f + 0.3f * Mathf.Sin(t * 12f);
            if (_beam != null)
                _beam.localScale = new Vector3(0.28f * pulse, 2.2f, 0.28f * pulse);
        }
    }
}
