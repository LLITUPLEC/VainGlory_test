using UnityEngine;

namespace Ashfold
{
    /// <summary>Верхняя грань Terrain / пола. Raycast работает с non-convex MeshCollider, в отличие от OverlapCapsule.</summary>
    public static class GroundProbe
    {
        public const float DefaultSurfaceY = 2f;
        const float RayLift = 24f;
        const float RayLen = 48f;
        static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static bool IsGround(Collider col)
        {
            if (col == null)
                return false;
            if (col is TerrainCollider)
                return true;
            var t = col.transform;
            while (t != null)
            {
                if (t.name == "Terrain" || t.name.StartsWith("Terrain"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        public static float SurfaceY(Vector3 pos, float fallback = DefaultSurfaceY)
        {
            var origin = new Vector3(pos.x, Mathf.Max(pos.y, DefaultSurfaceY) + RayLift, pos.z);
            var n = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, RayLen, ~0, QueryTriggerInteraction.Ignore);
            var terrainY = float.NegativeInfinity;
            var hitTerrain = false;
            for (var i = 0; i < n; i++)
            {
                var hit = Hits[i];
                if (hit.collider == null)
                    continue;
                if (hit.collider.GetComponentInParent<CombatUnit>() != null)
                    continue;
                if (!IsGround(hit.collider))
                    continue;
                hitTerrain = true;
                if (hit.point.y > terrainY)
                    terrainY = hit.point.y;
            }
            if (hitTerrain)
                return terrainY;
            return fallback;
        }

        public static Vector3 OnSurface(Vector3 pos, float hover)
        {
            pos.y = SurfaceY(pos) + hover;
            return pos;
        }

        public static void SitOnGround(Transform t, float skin = 0.04f)
        {
            if (t == null)
                return;
            var surface = SurfaceY(t.position);
            var bottom = t.position.y;
            var rends = t.GetComponentsInChildren<Renderer>();
            for (var i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null || !rends[i].enabled)
                    continue;
                bottom = Mathf.Min(bottom, rends[i].bounds.min.y);
            }
            t.position += Vector3.up * (surface - bottom + skin);
        }
    }
}
