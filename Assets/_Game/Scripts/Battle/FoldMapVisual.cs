using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfold
{
    /// <summary>
    /// Пустынный T-junction по map_fold: линия запад–восток, лес только на юге,
    /// каньон на севере. Используется как фоллбэк, пока нет префаба.
    /// </summary>
    public static class FoldMapVisual
    {
        public static FoldMap Build(Transform parent)
        {
            var root = new GameObject("FoldMap").transform;
            root.SetParent(parent, false);

            var sand = Mat(new Color(0.76f, 0.60f, 0.36f));
            var rock = Mat(new Color(0.40f, 0.32f, 0.26f));
            var cobble = Mat(new Color(0.68f, 0.64f, 0.56f));
            var wood = Mat(new Color(0.38f, 0.24f, 0.14f));
            var cactus = Mat(new Color(0.22f, 0.48f, 0.22f));
            var flower = Mat(new Color(0.92f, 0.32f, 0.55f));
            var roof = Mat(new Color(0.18f, 0.38f, 0.28f));
            var copper = Mat(new Color(0.78f, 0.42f, 0.18f));
            var plaster = Mat(new Color(0.82f, 0.72f, 0.55f));
            var teal = Mat(GameTheme.Teal);
            var crimson = Mat(GameTheme.Crimson);
            var portal = Mat(new Color(0.55f, 0.22f, 0.85f));
            var water = Mat(new Color(0.15f, 0.72f, 0.78f));
            var dark = Mat(new Color(0.08f, 0.06f, 0.05f));
            var blue = Mat(new Color(0.35f, 0.75f, 1f));
            var amber = Mat(new Color(1f, 0.72f, 0.2f));
            var camp = Mat(new Color(0.32f, 0.48f, 0.22f));
            var canyon = Mat(new Color(0.45f, 0.34f, 0.24f));

            Prim(root, PrimitiveType.Cube, new Vector3(0f, -0.4f, -6f), new Vector3(96f, 0.8f, 52f), sand, "Ground", true);
            Prim(root, PrimitiveType.Cube, new Vector3(0f, -4f, 28f), new Vector3(110f, 8f, 28f), canyon, "CanyonWall", false);
            Prim(root, PrimitiveType.Cube, new Vector3(0f, -8.5f, 48f), new Vector3(140f, 0.6f, 36f), water, "River", false);

            Prim(root, PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(78f, 0.16f, 8.6f), cobble, "Lane", false);
            Prim(root, PrimitiveType.Cube, new Vector3(0f, 0.08f, -12f), new Vector3(8.4f, 0.16f, 24f), cobble, "JungleRoad", false);
            Prim(root, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(11f, 0.08f, 11f), cobble, "Plaza", false);
            Prim(root, PrimitiveType.Cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(4f, 0.05f, 4f), rock, "PlazaStar", false);

            Prim(root, PrimitiveType.Cube, new Vector3(0f, 0.05f, -14f), new Vector3(52f, 0.08f, 18f), Mat(new Color(0.68f, 0.52f, 0.30f)), "JungleSouth", false);

            var gameplay = Group(root, "Gameplay");
            Marker(gameplay, "DawnSpawn", new Vector3(-FoldMapBuilder.HalfLength, 1.35f, 0f));
            Marker(gameplay, "DuskSpawn", new Vector3(FoldMapBuilder.HalfLength, 1.35f, 0f));
            Prim(gameplay, PrimitiveType.Cylinder, new Vector3(-16f, 1.6f, 0f), new Vector3(1.6f, 1.6f, 1.6f), teal, "TurretDawn", true);
            Prim(gameplay, PrimitiveType.Cylinder, new Vector3(16f, 1.6f, 0f), new Vector3(1.6f, 1.6f, 1.6f), crimson, "TurretDusk", true);
            var cd = Prim(gameplay, PrimitiveType.Cube, new Vector3(-FoldMapBuilder.HalfLength - 3.5f, 2.2f, 0f), new Vector3(1.4f, 4.2f, 1.4f), teal, "CrystalDawn", true);
            cd.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var ck = Prim(gameplay, PrimitiveType.Cube, new Vector3(FoldMapBuilder.HalfLength + 3.5f, 2.2f, 0f), new Vector3(1.4f, 4.2f, 1.4f), crimson, "CrystalDusk", true);
            ck.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            Prim(gameplay, PrimitiveType.Sphere, new Vector3(-12f, 0.55f, 13f), Vector3.one * 1.5f, camp, "CampNL", true);
            Prim(gameplay, PrimitiveType.Sphere, new Vector3(12f, 0.55f, 13f), Vector3.one * 1.5f, camp, "CampNR", true);
            Prim(gameplay, PrimitiveType.Sphere, new Vector3(-12f, 0.55f, -13f), Vector3.one * 1.5f, camp, "CampSL", true);
            Prim(gameplay, PrimitiveType.Sphere, new Vector3(12f, 0.55f, -13f), Vector3.one * 1.5f, camp, "CampSR", true);

            var scenery = Group(root, "Scenery");
            Prim(scenery, PrimitiveType.Cylinder, new Vector3(-38f, 0.12f, 0f), new Vector3(8.2f, 0.12f, 8.2f), cobble, "DawnPad", false);
            Prim(scenery, PrimitiveType.Cylinder, new Vector3(38f, 0.12f, 0f), new Vector3(8.2f, 0.12f, 8.2f), cobble, "DuskPad", false);
            Prim(scenery, PrimitiveType.Cube, new Vector3(-38f, 1.2f, -4.2f), new Vector3(3.2f, 2.4f, 1.2f), plaster, "DawnShop", true);
            Prim(scenery, PrimitiveType.Cube, new Vector3(38f, 1.2f, -4.2f), new Vector3(3.2f, 2.4f, 1.2f), plaster, "DuskShop", true);
            Prim(scenery, PrimitiveType.Cylinder, new Vector3(-42.5f, 0.22f, 0f), new Vector3(6.4f, 0.18f, 6.4f), rock, "PortalRing", false);
            Prim(scenery, PrimitiveType.Cylinder, new Vector3(-42.5f, 1.1f, 0f), new Vector3(4.2f, 0.08f, 4.2f), portal, "PortalGlow", false);

            House(scenery, new Vector3(-50f, 0f, 6f), 6.5f, 7.5f, 5.5f, plaster, roof, "DawnHouseA");
            House(scenery, new Vector3(-49f, 0f, -7.5f), 5f, 5.5f, 4.5f, plaster, roof, "DawnHouseB");
            Prim(scenery, PrimitiveType.Cylinder, new Vector3(48f, 3.2f, 3.5f), new Vector3(3.4f, 3.2f, 3.4f), plaster, "DuskTower", false);
            Prim(scenery, PrimitiveType.Sphere, new Vector3(48f, 6.6f, 3.5f), new Vector3(3.8f, 2.4f, 3.8f), copper, "DuskDome", false);
            Prim(scenery, PrimitiveType.Cube, new Vector3(47f, 1.1f, -3f), new Vector3(4.5f, 2.2f, 3.2f), wood, "DuskTent", false);

            Pit(scenery, new Vector3(-11f, 0f, -10.5f), dark, rock, blue, "PitL");
            Pit(scenery, new Vector3(11f, 0f, -10.5f), dark, rock, amber, "PitR");

            for (var i = 0; i <= 26; i++)
            {
                var x = Mathf.Lerp(-39f, 39f, i / 26f);
                Prim(scenery, PrimitiveType.Cube, new Vector3(x, 0.55f, 4.55f), new Vector3(2.6f, 0.12f, 0.12f), wood, "RailN", false);
            }
            for (var i = 0; i <= 28; i++)
            {
                var x = Mathf.Lerp(-40f, 40f, i / 28f);
                Prim(scenery, PrimitiveType.Cube, new Vector3(x, 0.85f, 13.4f), new Vector3(0.16f, 1.7f, 0.16f), wood, "Fence", false);
            }

            Stairs(scenery, new Vector3(-20f, 0f, -22.5f), cobble);
            Stairs(scenery, new Vector3(20f, 0f, -22.5f), cobble);

            Prim(scenery, PrimitiveType.Cylinder, new Vector3(-8f, 2f, 42f), new Vector3(3.2f, 8f, 3.2f), plaster, "FarTowerA", false);
            Prim(scenery, PrimitiveType.Cube, new Vector3(-8f, 10.4f, 42f), new Vector3(4.4f, 1.1f, 4.4f), roof, "FarTowerARoof", false);
            Prim(scenery, PrimitiveType.Cylinder, new Vector3(22f, 1.2f, 46f), new Vector3(2.6f, 7.2f, 2.6f), plaster, "FarTowerB", false);
            Prim(scenery, PrimitiveType.Sphere, new Vector3(22f, 8.6f, 46f), new Vector3(3.2f, 2.2f, 3.2f), copper, "FarTowerBDome", false);

            var foliage = Group(root, "Foliage");
            var rng = new System.Random(20260828);
            Vector3[] cactusSpots =
            {
                new Vector3(-8.2f, 0f, -8.4f), new Vector3(-6.4f, 0f, -10.8f), new Vector3(-10.5f, 0f, -7.2f),
                new Vector3(8.2f, 0f, -8.4f), new Vector3(6.4f, 0f, -10.8f), new Vector3(10.5f, 0f, -7.2f),
                new Vector3(-1.6f, 0f, -15.4f), new Vector3(2.1f, 0f, -16.6f), new Vector3(0.2f, 0f, -18.5f),
                new Vector3(-18.4f, 0f, -12.2f), new Vector3(-16.1f, 0f, -15f), new Vector3(-20.6f, 0f, -9.8f),
                new Vector3(18.4f, 0f, -12.2f), new Vector3(16.1f, 0f, -15f), new Vector3(20.6f, 0f, -9.8f),
                new Vector3(-13.8f, 0f, -6.2f), new Vector3(13.8f, 0f, -6.2f),
                new Vector3(-22.5f, 0f, -16.4f), new Vector3(22.5f, 0f, -16.4f),
                new Vector3(-5.4f, 0f, -20.2f), new Vector3(5.4f, 0f, -20.2f)
            };
            for (var i = 0; i < cactusSpots.Length; i++)
                Cactus(foliage, cactusSpots[i], 0.85f + (float)rng.NextDouble() * 0.45f, cactus, rng, i);

            Vector3[] flowers =
            {
                new Vector3(-9.5f, 0.35f, -6.5f), new Vector3(9.5f, 0.35f, -6.5f),
                new Vector3(-17f, 0.35f, -10f), new Vector3(17f, 0.35f, -10f),
                new Vector3(-3f, 0.35f, -17.5f), new Vector3(4f, 0.35f, -18f)
            };
            foreach (var p in flowers)
                Prim(foliage, PrimitiveType.Sphere, p, new Vector3(1.6f, 0.7f, 1.6f), flower, "Flower", false);

            var brushes = Group(root, "Brushes");
            Brush(brushes, new Vector3(-8f, 0.4f, -9f), 3.6f, "Brush_SL_Pit");
            Brush(brushes, new Vector3(8f, 0.4f, -9f), 3.6f, "Brush_SR_Pit");
            Brush(brushes, new Vector3(0f, 0.4f, -16.5f), 3.8f, "Brush_S");
            Brush(brushes, new Vector3(-18f, 0.4f, -13f), 3.5f, "Brush_SW");
            Brush(brushes, new Vector3(18f, 0.4f, -13f), 3.5f, "Brush_SE");
            Brush(brushes, new Vector3(-14f, 0.4f, -6.2f), 3.2f, "Brush_LaneL");
            Brush(brushes, new Vector3(14f, 0.4f, -6.2f), 3.2f, "Brush_LaneR");

            var auth = root.gameObject.AddComponent<FoldMapAuthoring>();
            auth.ApplyWorldSettings();
            return auth.ToFoldMap();
        }

        static void House(Transform parent, Vector3 pos, float w, float h, float d, Material wall, Material roof, string name)
        {
            Prim(parent, PrimitiveType.Cube, pos + new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), wall, name, false);
            Prim(parent, PrimitiveType.Cube, pos + new Vector3(0f, h + 0.45f, 0f), new Vector3(w + 0.8f, 0.7f, d + 0.8f), roof, name + "Roof", false);
        }

        static void Pit(Transform parent, Vector3 c, Material dark, Material rock, Material crystal, string name)
        {
            Prim(parent, PrimitiveType.Cylinder, c + new Vector3(0f, -1.4f, 0f), new Vector3(9.2f, 1.6f, 9.2f), dark, name, false);
            var cr = Prim(parent, PrimitiveType.Cube, c + new Vector3(3.8f, 0.9f, 2.2f), new Vector3(0.9f, 1.8f, 0.9f), crystal, name + "Crystal", false);
            cr.transform.rotation = Quaternion.Euler(12f, 35f, 18f);
            for (var i = 0; i < 6; i++)
            {
                var a = i / 6f * Mathf.PI * 2f;
                Prim(parent, PrimitiveType.Cube, c + new Vector3(Mathf.Cos(a) * 5f, 0.4f, Mathf.Sin(a) * 5f),
                    new Vector3(1.5f, 0.6f, 1f), rock, name + "Rim", false);
            }
        }

        static void Stairs(Transform parent, Vector3 origin, Material mat)
        {
            for (var i = 0; i < 6; i++)
            {
                Prim(parent, PrimitiveType.Cube, origin + new Vector3(0f, -0.28f * i, -1.05f * i),
                    new Vector3(5.5f, 0.28f, 1.15f), mat, "Step", false);
            }
        }

        static void Cactus(Transform parent, Vector3 pos, float s, Material mat, System.Random rng, int id)
        {
            var root = Group(parent, "Cactus_" + id);
            root.position = pos;
            Prim(root, PrimitiveType.Cylinder, new Vector3(0f, 1.15f * s, 0f), new Vector3(0.38f * s, 1.15f * s, 0.38f * s), mat, "Stem", false);
            var arms = 2 + rng.Next(2);
            for (var i = 0; i < arms; i++)
            {
                var side = i % 2 == 0 ? 1f : -1f;
                var y = (0.7f + (float)rng.NextDouble() * 0.7f) * s;
                Prim(root, PrimitiveType.Cylinder, new Vector3(side * 0.42f * s, y, 0f),
                    new Vector3(0.55f * s, 0.16f * s, 0.16f * s), mat, "ArmH", false);
                Prim(root, PrimitiveType.Cylinder, new Vector3(side * 0.68f * s, y + 0.45f * s, 0f),
                    new Vector3(0.16f * s, 0.45f * s, 0.16f * s), mat, "ArmV", false);
            }
        }

        static void Brush(Transform parent, Vector3 pos, float radius, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<BrushZone>().Radius = radius;
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Marker(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
        }

        static Material Mat(Color c)
        {
            return RuntimeMat.Make(c);
        }

        static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, string name, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.On;
            if (!solid)
            {
                var col = go.GetComponent<Collider>();
                if (col == null) { }
                else if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
            return go;
        }
    }
}
