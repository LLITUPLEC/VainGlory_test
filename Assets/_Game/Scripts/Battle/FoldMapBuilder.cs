using UnityEngine;
using UnityEngine.Rendering;

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

    /// <summary>Серый бокс VG 3v3: мид слева-направо, лес сверху/снизу, 1 турель + кристалл.</summary>
    public static class FoldMapBuilder
    {
        public const float HalfLength = 38f;
        public const float HalfWidth = 22f;
        public const float FountainRadius = 7.4f;

        public static bool InFountain(Vector3 pos, TeamId team)
        {
            var x = team == TeamId.Dusk ? HalfLength : -HalfLength;
            pos.y = 0f;
            return Vector3.Distance(pos, new Vector3(x, 0f, 0f)) <= FountainRadius;
        }

        public static FoldMap Build(Transform parent)
        {
            var map = new FoldMap
            {
                DawnSpawn = new Vector3(-HalfLength, 1.35f, 0f),
                DuskSpawn = new Vector3(HalfLength, 1.35f, 0f)
            };
            var root = new GameObject("FoldMap").transform;
            root.SetParent(parent, false);
            map.Root = root;

            // Толстый пол: верх грани = y=0. Тонкие кубы на мобилках всплывают из-за depth/теней.
            Prim(root, PrimitiveType.Cube, new Vector3(0f, -0.4f, 0f), new Vector3(84f, 0.8f, 48f), GameTheme.Hex(0x1A2A28), "Ground", true);
            Decal(root, new Vector3(0f, 0f, 0f), new Vector2(76f, 9f), GameTheme.Hex(0x3A4A3C), "Lane");
            Decal(root, new Vector3(0f, 0f, 14f), new Vector2(50f, 14f), GameTheme.Hex(0x15241C), "JungleNorth");
            Decal(root, new Vector3(0f, 0f, -14f), new Vector2(50f, 14f), GameTheme.Hex(0x15241C), "JungleSouth");

            map.Camps = new[]
            {
                Prim(root, PrimitiveType.Sphere, new Vector3(-12f, 0.4f, 13f), Vector3.one * 1.4f, GameTheme.Hex(0x4A6A38), "CampNL", true),
                Prim(root, PrimitiveType.Sphere, new Vector3(12f, 0.4f, 13f), Vector3.one * 1.4f, GameTheme.Hex(0x4A6A38), "CampNR", true),
                Prim(root, PrimitiveType.Sphere, new Vector3(-12f, 0.4f, -13f), Vector3.one * 1.4f, GameTheme.Hex(0x4A6A38), "CampSL", true),
                Prim(root, PrimitiveType.Sphere, new Vector3(12f, 0.4f, -13f), Vector3.one * 1.4f, GameTheme.Hex(0x4A6A38), "CampSR", true)
            };

            Base(root, -HalfLength, GameTheme.Teal, "Dawn");
            Base(root, HalfLength, GameTheme.Crimson, "Dusk");

            map.TurretDawn = Prim(root, PrimitiveType.Cylinder, new Vector3(-16f, 1.6f, 0f), new Vector3(1.6f, 1.6f, 1.6f), GameTheme.Teal, "TurretDawn", true);
            map.TurretDusk = Prim(root, PrimitiveType.Cylinder, new Vector3(16f, 1.6f, 0f), new Vector3(1.6f, 1.6f, 1.6f), GameTheme.Crimson, "TurretDusk", true);

            map.CrystalDawn = Prim(root, PrimitiveType.Cube, new Vector3(-HalfLength - 3.5f, 2.2f, 0f), new Vector3(1.4f, 4.2f, 1.4f), GameTheme.Teal, "CrystalDawn", true);
            map.CrystalDawn.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            map.CrystalDusk = Prim(root, PrimitiveType.Cube, new Vector3(HalfLength + 3.5f, 2.2f, 0f), new Vector3(1.4f, 4.2f, 1.4f), GameTheme.Crimson, "CrystalDusk", true);
            map.CrystalDusk.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            Brush(root, new Vector3(-8f, 0.6f, 6.5f), 3.4f, "BrushNL");
            Brush(root, new Vector3(8f, 0.6f, 6.5f), 3.4f, "BrushNR");
            Brush(root, new Vector3(-8f, 0.6f, -6.5f), 3.4f, "BrushSL");
            Brush(root, new Vector3(8f, 0.6f, -6.5f), 3.4f, "BrushSR");
            Brush(root, new Vector3(0f, 0.6f, 11f), 3.8f, "BrushN");
            Brush(root, new Vector3(0f, 0.6f, -11f), 3.8f, "BrushS");

            return map;
        }

        static void Brush(Transform root, Vector3 pos, float radius, string name)
        {
            var go = Prim(root, PrimitiveType.Cylinder, pos, new Vector3(radius * 2f, 0.35f, radius * 2f), GameTheme.Hex(0x2A4A30, 0.85f), name, false);
            var zone = go.AddComponent<BrushZone>();
            zone.Radius = radius;
        }

        static void Base(Transform root, float x, Color color, string name)
        {
            Prim(root, PrimitiveType.Cylinder, new Vector3(x, 0.15f, 0f), new Vector3(6.5f, 0.15f, 6.5f), Color.Lerp(color, GameTheme.Bg, 0.55f), name + "Pad", false);
            Prim(root, PrimitiveType.Cube, new Vector3(x, 1.2f, -4.2f), new Vector3(3.2f, 2.4f, 1.2f), GameTheme.BgPanel, name + "Shop", true);
        }

        static GameObject Decal(Transform parent, Vector3 pos, Vector2 size, Color color, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, 0.02f, pos.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            Object.Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = RuntimeMat.Make(color);
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return go;
        }

        static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, string name, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = RuntimeMat.Make(color);
            rend.shadowCastingMode = ShadowCastingMode.Off;
            if (!solid)
                Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
    }
}
