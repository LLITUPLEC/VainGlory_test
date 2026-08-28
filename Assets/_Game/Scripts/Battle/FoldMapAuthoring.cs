using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfold
{
    /// <summary>Якоря геймплея на префабе карты. Кусты (BrushZone) расставляются вручную по кактусам.</summary>
    public sealed class FoldMapAuthoring : MonoBehaviour
    {
        public Transform DawnSpawnPoint;
        public Transform DuskSpawnPoint;
        public GameObject TurretDawn;
        public GameObject TurretDusk;
        public GameObject CrystalDawn;
        public GameObject CrystalDusk;
        public GameObject[] Camps;

        public FoldMap ToFoldMap()
        {
            ResolveIfNeeded();
            return new FoldMap
            {
                Root = transform,
                TurretDawn = TurretDawn,
                TurretDusk = TurretDusk,
                CrystalDawn = CrystalDawn,
                CrystalDusk = CrystalDusk,
                Camps = Camps,
                DawnSpawn = DawnSpawnPoint != null
                    ? DawnSpawnPoint.position
                    : new Vector3(-FoldMapBuilder.HalfLength, 1.35f, 0f),
                DuskSpawn = DuskSpawnPoint != null
                    ? DuskSpawnPoint.position
                    : new Vector3(FoldMapBuilder.HalfLength, 1.35f, 0f)
            };
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
            if (TurretDawn == null) TurretDawn = FindChild("TurretDawn");
            if (TurretDusk == null) TurretDusk = FindChild("TurretDusk");
            if (CrystalDawn == null) CrystalDawn = FindChild("CrystalDawn");
            if (CrystalDusk == null) CrystalDusk = FindChild("CrystalDusk");
            if (DawnSpawnPoint == null)
            {
                var go = FindChild("DawnSpawn");
                if (go != null) DawnSpawnPoint = go.transform;
            }
            if (DuskSpawnPoint == null)
            {
                var go = FindChild("DuskSpawn");
                if (go != null) DuskSpawnPoint = go.transform;
            }
            if (Camps == null || Camps.Length == 0)
            {
                Camps = new[]
                {
                    FindChild("CampNL"),
                    FindChild("CampNR"),
                    FindChild("CampSL"),
                    FindChild("CampSR")
                };
            }
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
