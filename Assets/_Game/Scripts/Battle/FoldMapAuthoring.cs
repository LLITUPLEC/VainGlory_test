using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfold
{
    /// <summary>Якоря геймплея на префабе карты. Кусты (BrushZone) расставляются вручную.</summary>
    public sealed class FoldMapAuthoring : MonoBehaviour
    {
        public Transform DawnSpawnPoint;
        public Transform DuskSpawnPoint;
        [Tooltip("Необязательно: если пусто, ищется Turret_L_1 / TurretDawn.")]
        public GameObject TurretDawn;
        [Tooltip("Необязательно: если пусто, ищется Turret_R_1 / TurretDusk.")]
        public GameObject TurretDusk;
        [Tooltip("Необязательно: если пусто, ищутся Turret_L_1…5 по имени.")]
        public GameObject[] TurretsDawn;
        [Tooltip("Необязательно: если пусто, ищутся Turret_R_1…5 по имени.")]
        public GameObject[] TurretsDusk;
        [Tooltip("Необязательно: если пусто, ищется CrystalDawn.")]
        public GameObject CrystalDawn;
        [Tooltip("Необязательно: если пусто, ищется CrystalDusk.")]
        public GameObject CrystalDusk;
        [Tooltip("Необязательно: если пусто, ищутся Camp_L_1/2 и Camp_R_1/2.")]
        public GameObject[] Camps;
        [Tooltip("Точка Гвоздожуя. Если пусто, ищется Boss_p.")]
        public GameObject Boss;
        [Tooltip("Точки линии слева направо (Lane_1…). Если пусто — турели *_3/_4/_5.")]
        public Transform[] LanePoints;

        public FoldMap ToFoldMap()
        {
            ResolveIfNeeded();
            var dawnTurrets = NonNull(TurretsDawn);
            var duskTurrets = NonNull(TurretsDusk);
            var lane = LaneWorldPoints();
            return new FoldMap
            {
                Root = transform,
                TurretsDawn = dawnTurrets,
                TurretsDusk = duskTurrets,
                TurretDawn = dawnTurrets.Length > 0 ? dawnTurrets[0] : TurretDawn,
                TurretDusk = duskTurrets.Length > 0 ? duskTurrets[0] : TurretDusk,
                CrystalDawn = CrystalDawn,
                CrystalDusk = CrystalDusk,
                Camps = NonNull(Camps),
                Boss = Boss,
                DawnSpawn = PlayableSpawn(DawnSpawnPoint, DuskSpawnPoint, FoldMapBuilder.DawnFountain),
                DuskSpawn = PlayableSpawn(DuskSpawnPoint, DawnSpawnPoint, FoldMapBuilder.DuskFountain),
                LanePath = lane
            };
        }

        Vector3[] LaneWorldPoints()
        {
            if (LanePoints == null || LanePoints.Length == 0)
            {
                var found = FindNumbered("Lane_");
                if (found.Length > 0)
                {
                    var fromNames = new Vector3[found.Length];
                    for (var i = 0; i < found.Length; i++)
                        fromNames[i] = found[i].transform.position;
                    return fromNames;
                }
                return System.Array.Empty<Vector3>();
            }
            var list = new List<Vector3>(LanePoints.Length);
            for (var i = 0; i < LanePoints.Length; i++)
            {
                if (LanePoints[i] != null)
                    list.Add(LanePoints[i].position);
            }
            return list.ToArray();
        }

        static Vector3 PlayableSpawn(Transform marker, Transform toward, Vector3 fallback)
        {
            if (marker == null)
                return fallback;
            var p = FoldMapBuilder.Flatten(marker.position);
            if (toward == null)
                return p;
            var d = toward.position - p;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f)
                return p;
            return FoldMapBuilder.Flatten(p + d.normalized * FoldMapBuilder.SpawnInset);
        }

        public void ApplyWorldSettings()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.62f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.40f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.16f, 0.10f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.80f, 0.64f, 0.42f);
            RenderSettings.fogStartDistance = 48f;
            RenderSettings.fogEndDistance = 160f;
        }

        void ResolveIfNeeded()
        {
            if (CrystalDawn == null) CrystalDawn = FindChild("CrystalDawn");
            if (CrystalDusk == null) CrystalDusk = FindChild("CrystalDusk");

            DawnSpawnPoint = EnsureSpawn(DawnSpawnPoint, "DawnSpawn", "Stairs_L");
            DuskSpawnPoint = EnsureSpawn(DuskSpawnPoint, "DuskSpawn", "Stairs_R");

            if (TurretsDawn == null || TurretsDawn.Length == 0)
            {
                TurretsDawn = FindNumbered("Turret_L_");
                if (TurretsDawn.Length == 0 && TurretDawn == null)
                    TurretDawn = FindChild("TurretDawn");
                if (TurretsDawn.Length == 0 && TurretDawn != null)
                    TurretsDawn = new[] { TurretDawn };
            }

            if (TurretsDusk == null || TurretsDusk.Length == 0)
            {
                TurretsDusk = FindNumbered("Turret_R_");
                if (TurretsDusk.Length == 0 && TurretDusk == null)
                    TurretDusk = FindChild("TurretDusk");
                if (TurretsDusk.Length == 0 && TurretDusk != null)
                    TurretsDusk = new[] { TurretDusk };
            }

            if (Camps == null || Camps.Length == 0)
            {
                Camps = Collect(
                    FindChild("Camp_L_1"), FindChild("Camp_L_2"),
                    FindChild("Camp_R_1"), FindChild("Camp_R_2"));
                if (Camps.Length == 0)
                {
                    Camps = Collect(
                        FindChild("CampNL"), FindChild("CampNR"),
                        FindChild("CampSL"), FindChild("CampSR"));
                }
            }

            if (Boss == null)
                Boss = FindChild("Boss_p") ?? FindChild("Boss");
        }

        Transform EnsureSpawn(Transform current, string markerName, string stairsName)
        {
            if (current != null)
                return current;
            var marker = FindChild(markerName);
            if (marker != null)
                return marker.transform;
            var stairs = FindChild(stairsName);
            if (stairs == null)
                return null;
            var go = new GameObject(markerName);
            go.transform.SetParent(stairs.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        GameObject[] FindNumbered(string prefix)
        {
            var list = new List<GameObject>(8);
            for (var i = 1; i <= 8; i++)
            {
                var go = FindChild(prefix + i);
                if (go != null)
                    list.Add(go);
            }
            return list.ToArray();
        }

        static GameObject[] Collect(params GameObject[] items)
        {
            var list = new List<GameObject>(items.Length);
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                    list.Add(items[i]);
            }
            return list.ToArray();
        }

        static GameObject[] NonNull(GameObject[] src)
        {
            if (src == null || src.Length == 0)
                return System.Array.Empty<GameObject>();
            var list = new List<GameObject>(src.Length);
            for (var i = 0; i < src.Length; i++)
            {
                if (src[i] != null)
                    list.Add(src[i]);
            }
            return list.ToArray();
        }

        GameObject FindChild(string name)
        {
            var t = transform.Find(name);
            if (t != null) return t.gameObject;
            foreach (var tr in GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == name)
                    return tr.gameObject;
            }
            return null;
        }
    }
}
