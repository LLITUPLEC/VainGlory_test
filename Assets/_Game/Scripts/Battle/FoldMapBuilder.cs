using UnityEngine;

namespace Ashfold
{
    public sealed class FoldMap
    {
        public Transform Root;
        public GameObject TurretDawn;
        public GameObject TurretDusk;
        public GameObject[] TurretsDawn;
        public GameObject[] TurretsDusk;
        public GameObject CrystalDawn;
        public GameObject CrystalDusk;
        public GameObject[] Camps;
        public GameObject Boss;
        public Vector3 DawnSpawn;
        public Vector3 DuskSpawn;
        public Vector3[] LanePath;
    }

    /// <summary>
    /// Карта: префаб Resources/Maps/FoldMap_new. Старый FoldMap остаётся в проекте.
    /// Авторский лейаут вдоль Z; в бою корень поворачивается на PlayYaw, линия слева–направо.
    /// </summary>
    public static class FoldMapBuilder
    {
        public const string PrefabResource = "Maps/FoldMap_new";
        public const int NetCrystalDawn = 12;
        public const int NetCrystalDusk = 13;
        public const int NetCamp0 = 20;
        public const int NetBoss = 24;
        public const int NetTurretDawn0 = 30;
        public const int NetTurretDusk0 = 35;
        public const float HeroGroundY = 1.35f;
        public const float PlayYaw = -90f;
        public const float SpawnInset = 14f;
        public const float LegacyHalfLength = 38f;
        public const float LegacyHalfWidth = 22f;

        public static float HalfLength = 130f;
        public static float HalfWidth = 40f;
        public static float FountainRadius = 14f;
        public static Vector3 DawnFountain = new Vector3(-69.53f, HeroGroundY, 13.73f);
        public static Vector3 DuskFountain = new Vector3(98.68f, HeroGroundY, 13.44f);
        public static Vector3 DawnCrystal = new Vector3(-77.24f, HeroGroundY, -13.34f);
        public static Vector3 DuskCrystal = new Vector3(103.20f, HeroGroundY, -10.87f);
        public static Vector3[] LanePoints = System.Array.Empty<Vector3>();

        public static bool InFountain(Vector3 pos, TeamId team)
        {
            var f = team == TeamId.Dusk ? DuskFountain : DawnFountain;
            pos.y = 0f;
            f.y = 0f;
            return Vector3.Distance(pos, f) <= FountainRadius;
        }

        public static Vector3 ClampPlayable(Vector3 world)
        {
            world.x = Mathf.Clamp(world.x, -HalfLength - 2f, HalfLength + 2f);
            world.z = Mathf.Clamp(world.z, -HalfWidth, HalfWidth);
            return world;
        }

        public static Vector3 LaneDir(TeamId team)
        {
            if (LanePoints != null && LanePoints.Length >= 2)
            {
                var a = LanePoints[0];
                var b = LanePoints[LanePoints.Length - 1];
                var d = team == TeamId.Dawn ? b - a : a - b;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    return d.normalized;
            }
            var from = team == TeamId.Dawn ? DawnCrystal : DuskCrystal;
            var to = team == TeamId.Dawn ? DuskCrystal : DawnCrystal;
            var fallback = to - from;
            fallback.y = 0f;
            if (fallback.sqrMagnitude < 0.01f)
                return team == TeamId.Dawn ? Vector3.right : Vector3.left;
            return fallback.normalized;
        }

        public static Vector3 LaneOrigin(TeamId team)
        {
            if (LanePoints != null && LanePoints.Length >= 2)
            {
                if (team == TeamId.Dawn)
                    return LanePoints[0] + (LanePoints[1] - LanePoints[0]).normalized * 4f;
                var last = LanePoints.Length - 1;
                return LanePoints[last] + (LanePoints[last - 1] - LanePoints[last]).normalized * 4f;
            }
            var crystal = team == TeamId.Dawn ? DawnCrystal : DuskCrystal;
            return crystal + LaneDir(team) * 7f;
        }

        public static Vector3 LaneGoal(TeamId team)
        {
            if (LanePoints != null && LanePoints.Length > 0)
                return team == TeamId.Dawn ? LanePoints[LanePoints.Length - 1] : LanePoints[0];
            return team == TeamId.Dawn ? DuskFountain : DawnFountain;
        }

        public static Vector3 NextLanePoint(TeamId team, Vector3 pos)
        {
            var idx = -1;
            return NextCommitted(team, pos, ref idx);
        }

        public static Vector3 NextCommitted(TeamId team, Vector3 pos, ref int index)
        {
            var pts = LanePoints;
            if (pts == null || pts.Length == 0)
                return LaneGoal(team);
            if (team == TeamId.Dawn)
            {
                if (index < 0)
                    index = FirstAhead(pts, pos, true);
                while (index < pts.Length - 1 && ReachedLanePoint(pos, pts[index], true))
                    index++;
                index = Mathf.Clamp(index, 0, pts.Length - 1);
                return pts[index];
            }
            if (index < 0)
                index = FirstAhead(pts, pos, false);
            while (index > 0 && ReachedLanePoint(pos, pts[index], false))
                index--;
            index = Mathf.Clamp(index, 0, pts.Length - 1);
            return pts[index];
        }

        static bool ReachedLanePoint(Vector3 pos, Vector3 pt, bool dawn)
        {
            if (DistFlat(pos, pt) <= 2.2f && Mathf.Abs(pos.x - pt.x) <= 2.2f)
                return true;
            return dawn ? pos.x >= pt.x + 0.4f : pos.x <= pt.x - 0.4f;
        }

        static int FirstAhead(Vector3[] pts, Vector3 pos, bool dawn)
        {
            if (dawn)
            {
                for (var i = 0; i < pts.Length; i++)
                {
                    if (pts[i].x >= pos.x - 2f)
                        return i;
                }
                return pts.Length - 1;
            }
            for (var i = pts.Length - 1; i >= 0; i--)
            {
                if (pts[i].x <= pos.x + 2f)
                    return i;
            }
            return 0;
        }

        public static FoldMap Build(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(PrefabResource);
            FoldMap map;
            if (prefab != null)
                map = InstantiatePrefab(prefab, parent);
            else
            {
                Debug.LogWarning("[Ashfold] FoldMap_new prefab missing — desert fallback.");
                map = FoldMapVisual.Build(parent);
            }
            CaptureLayout(map);
            return map;
        }

        static FoldMap InstantiatePrefab(GameObject prefab, Transform parent)
        {
            var go = Object.Instantiate(prefab, parent, false);
            go.name = "FoldMap";
            go.transform.Rotate(0f, PlayYaw, 0f, Space.Self);
            var auth = go.GetComponent<FoldMapAuthoring>() ?? go.AddComponent<FoldMapAuthoring>();
            auth.ApplyWorldSettings();
            return auth.ToFoldMap();
        }

        static void CaptureLayout(FoldMap map)
        {
            if (map == null)
                return;
            Physics.SyncTransforms();
            DawnFountain = Flatten(map.DawnSpawn);
            DuskFountain = Flatten(map.DuskSpawn);
            if (map.CrystalDawn != null)
                DawnCrystal = Flatten(map.CrystalDawn.transform.position);
            if (map.CrystalDusk != null)
                DuskCrystal = Flatten(map.CrystalDusk.transform.position);

            var minX = Mathf.Min(DawnFountain.x, DuskFountain.x, DawnCrystal.x, DuskCrystal.x);
            var maxX = Mathf.Max(DawnFountain.x, DuskFountain.x, DawnCrystal.x, DuskCrystal.x);
            var minZ = Mathf.Min(DawnFountain.z, DuskFountain.z, DawnCrystal.z, DuskCrystal.z);
            var maxZ = Mathf.Max(DawnFountain.z, DuskFountain.z, DawnCrystal.z, DuskCrystal.z);
            Encapsulate(map.TurretsDawn, ref minX, ref maxX, ref minZ, ref maxZ);
            Encapsulate(map.TurretsDusk, ref minX, ref maxX, ref minZ, ref maxZ);
            Encapsulate(map.Camps, ref minX, ref maxX, ref minZ, ref maxZ);
            const float pad = 16f;
            HalfLength = Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX)) + pad;
            HalfWidth = Mathf.Max(Mathf.Abs(minZ), Mathf.Abs(maxZ)) + pad;
            CaptureLane(map);
        }

        static void CaptureLane(FoldMap map)
        {
            var pts = new System.Collections.Generic.List<Vector3>(12);
            if (map.LanePath != null && map.LanePath.Length > 0)
            {
                for (var i = 0; i < map.LanePath.Length; i++)
                    pts.Add(Flatten(map.LanePath[i]));
            }
            else
            {
                CollectLaneTurrets(map.TurretsDawn, pts);
                CollectLaneTurrets(map.TurretsDusk, pts);
                pts.Sort((a, b) => a.x.CompareTo(b.x));
            }
            if (pts.Count == 0)
            {
                pts.Add(DawnCrystal);
                pts.Add(DuskCrystal);
            }
            else
            {
                if (DistFlat(DawnCrystal, pts[0]) > 5f)
                    pts.Insert(0, DawnCrystal);
                if (DistFlat(DuskCrystal, pts[pts.Count - 1]) > 5f)
                    pts.Add(DuskCrystal);
            }
            LanePoints = pts.ToArray();
        }

        static void CollectLaneTurrets(GameObject[] turrets, System.Collections.Generic.List<Vector3> dst)
        {
            if (turrets == null)
                return;
            for (var i = 0; i < turrets.Length; i++)
            {
                var go = turrets[i];
                if (go == null)
                    continue;
                var n = go.name;
                if (n.EndsWith("_3") || n.EndsWith("_4") || n.EndsWith("_5"))
                    dst.Add(Flatten(go.transform.position + new Vector3(0f, 0f, 3.8f)));
            }
        }

        static float DistFlat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static void Encapsulate(GameObject[] gos, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            if (gos == null)
                return;
            for (var i = 0; i < gos.Length; i++)
            {
                if (gos[i] == null)
                    continue;
                var p = gos[i].transform.position;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }
        }

        public static Vector3 Flatten(Vector3 p)
        {
            return GroundProbe.OnSurface(p, HeroGroundY);
        }
    }
}
