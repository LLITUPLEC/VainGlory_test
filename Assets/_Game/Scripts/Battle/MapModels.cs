using UnityEngine;

namespace Ashfold
{
    /// <summary>Ресурсные префабы арт-моделей карты. Пекутся через Ashfold → Setup Map Models.</summary>
    public static class MapModels
    {
        public const string TurretRes = "Maps/Parts/Turret";
        public const string CrystalRes = "Maps/Parts/Crystal";
        public const string MinionVisualRes = "Units/MinionVisual";
        public const string CaptainVisualRes = "Units/CaptainVisual";
        public const string BossVisualRes = "Units/BossVisual";

        public static GameObject TryPlace(string resource, Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            var prefab = Resources.Load<GameObject>(resource);
            if (prefab == null)
                return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;
            return go;
        }
    }
}
