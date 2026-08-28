using UnityEngine;

namespace Ashfold
{
    public sealed class FoldMap
    {
        public Transform Root;
        public GameObject TurretDawn;
        public GameObject TurretDusk;
        public GameObject CrystalDawn;
        public GameObject CrystalDusk;
        public GameObject[] Camps;
        public Vector3 DawnSpawn;
        public Vector3 DuskSpawn;
    }

    /// <summary>
    /// Карта: префаб Resources/Maps/FoldMap (пустыня, T-junction, лес только с юга).
    /// Геймплейные координаты совпадают с сервером. Пока префаба нет — FoldMapVisual.
    /// </summary>
    public static class FoldMapBuilder
    {
        public const float HalfLength = 38f;
        public const float HalfWidth = 22f;
        public const float FountainRadius = 7.4f;
        public const string PrefabResource = "Maps/FoldMap";

        public static bool InFountain(Vector3 pos, TeamId team)
        {
            var x = team == TeamId.Dusk ? HalfLength : -HalfLength;
            pos.y = 0f;
            return Vector3.Distance(pos, new Vector3(x, 0f, 0f)) <= FountainRadius;
        }

        public static FoldMap Build(Transform parent)
        {
            var prefab = Resources.Load<GameObject>(PrefabResource);
            if (prefab != null)
                return InstantiatePrefab(prefab, parent);
            Debug.LogWarning("[Ashfold] FoldMap prefab missing — desert fallback. In Unity: Ashfold → Bake Fold Map Prefab");
            return FoldMapVisual.Build(parent);
        }

        static FoldMap InstantiatePrefab(GameObject prefab, Transform parent)
        {
            var go = Object.Instantiate(prefab, parent, false);
            go.name = "FoldMap";
            var auth = go.GetComponent<FoldMapAuthoring>() ?? go.AddComponent<FoldMapAuthoring>();
            auth.ApplyWorldSettings();
            return auth.ToFoldMap();
        }
    }
}
