using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ashfold.Editor
{
    /// <summary>
    /// Печёт префаб карты по map_fold.jpeg: пустынный T-junction, каньон на севере,
    /// лес/кактусы только на юге. Геймплейные точки как на сервере.
    /// Меню: Ashfold → Bake Fold Map Prefab. Кусты потом двигайте в префабе (гизмо сфер).
    /// </summary>
    public static class FoldMapPrefabBaker
    {
        const string ArtRoot = "Assets/_Game/Art/Map";
        const string TexDir = ArtRoot + "/Textures";
        const string MatDir = ArtRoot + "/Materials";
        const string LayerDir = ArtRoot + "/Terrain/Layers";
        const string TerrainPath = ArtRoot + "/Terrain/FoldTerrain.asset";
        const string PrefabPath = "Assets/_Game/Resources/Maps/FoldMap.prefab";
        const string TerrainLitPath = MatDir + "/FoldTerrainLit.mat";

        const int HeightRes = 513;
        const int AlphaRes = 512;
        const float SizeX = 200f;
        const float SizeZ = 180f;
        const float SizeY = 28f;
        const float TerrainY = -12f;
        const float TerrainX = -100f;
        const float TerrainZ = -70f;
        const float Play = 12f / SizeY;

        static Material _sand, _rock, _cobble, _wood, _cactus, _flower, _roof, _copper, _plaster;
        static Material _teal, _crimson, _portal, _water, _dark, _blueCrystal, _amberCrystal, _camp;

        [MenuItem("Ashfold/Bake Fold Map Prefab")]
        public static void BakeFromMenu()
        {
            Bake();
            EditorUtility.DisplayDialog("Ashfold", "Префаб карты записан:\n" + PrefabPath + "\n\nДальше: Prefab Mode → Terrain Tools (выступы), сдвиньте Brush_* на кактусы.", "OK");
        }

        public static void BakeFromCommandLine()
        {
            Bake();
        }

        public static void Bake()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Fold Map", "Папки и текстуры", 0.05f);
                EnsureFolders();
                var sandTex = MakeTex("tex_sand.png", new Color(0.78f, 0.62f, 0.38f), new Color(0.62f, 0.48f, 0.28f), 0.16f);
                var rockTex = MakeTex("tex_rock.png", new Color(0.42f, 0.34f, 0.28f), new Color(0.28f, 0.24f, 0.20f), 0.22f);
                var cobbleTex = MakeCobble("tex_cobble.png");
                var canyonTex = MakeTex("tex_canyon.png", new Color(0.35f, 0.28f, 0.22f), new Color(0.18f, 0.14f, 0.12f), 0.2f);

                EditorUtility.DisplayProgressBar("Fold Map", "Материалы", 0.15f);
                MakeMaterials();

                EditorUtility.DisplayProgressBar("Fold Map", "Terrain", 0.3f);
                var layers = new[]
                {
                    MakeLayer("layer_sand.terrainlayer", sandTex, 14f),
                    MakeLayer("layer_rock.terrainlayer", rockTex, 18f),
                    MakeLayer("layer_cobble.terrainlayer", cobbleTex, 8f),
                    MakeLayer("layer_canyon.terrainlayer", canyonTex, 22f)
                };
                var data = BuildTerrainData(layers);
                AssetDatabase.CreateAsset(data, TerrainPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("Fold Map", "Иерархия", 0.55f);
                var root = new GameObject("FoldMap");
                try
                {
                    var terrainGo = Terrain.CreateTerrainGameObject(data);
                    terrainGo.name = "Terrain";
                    terrainGo.transform.SetParent(root.transform, false);
                    terrainGo.transform.localPosition = new Vector3(TerrainX, TerrainY, TerrainZ);
                    var terrain = terrainGo.GetComponent<Terrain>();
                    terrain.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>(TerrainLitPath);
                    terrain.drawInstanced = true;
                    terrain.heightmapPixelError = 8f;
                    terrain.basemapDistance = 200f;
                    var col = terrainGo.GetComponent<TerrainCollider>();
                    if (col != null) col.terrainData = data;

                    var gameplay = Group(root.transform, "Gameplay");
                    var scenery = Group(root.transform, "Scenery");
                    var foliage = Group(root.transform, "Foliage");
                    var brushes = Group(root.transform, "Brushes");

                    PlaceGameplay(gameplay);
                    PlaceLaneDressing(scenery);
                    PlaceBases(scenery);
                    PlacePits(scenery);
                    PlaceStairs(scenery);
                    PlaceFence(scenery);
                    PlaceFoliage(foliage);
                    PlaceBrushes(brushes);
                    PlaceCanyonVista(scenery);

                    var auth = root.AddComponent<FoldMapAuthoring>();
                    auth.DawnSpawnPoint = gameplay.Find("DawnSpawn");
                    auth.DuskSpawnPoint = gameplay.Find("DuskSpawn");
                    auth.TurretDawn = gameplay.Find("TurretDawn").gameObject;
                    auth.TurretDusk = gameplay.Find("TurretDusk").gameObject;
                    auth.CrystalDawn = gameplay.Find("CrystalDawn").gameObject;
                    auth.CrystalDusk = gameplay.Find("CrystalDusk").gameObject;
                    auth.Camps = new[]
                    {
                        gameplay.Find("CampNL").gameObject,
                        gameplay.Find("CampNR").gameObject,
                        gameplay.Find("CampSL").gameObject,
                        gameplay.Find("CampSR").gameObject
                    };

                    EditorUtility.DisplayProgressBar("Fold Map", "Prefab", 0.9f);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Ashfold] FoldMap prefab baked → " + PrefabPath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void EnsureFolders()
        {
            CreateFolder("Assets/_Game", "Art");
            CreateFolder("Assets/_Game/Art", "Map");
            CreateFolder(ArtRoot, "Textures");
            CreateFolder(ArtRoot, "Materials");
            CreateFolder(ArtRoot, "Terrain");
            CreateFolder(ArtRoot + "/Terrain", "Layers");
            CreateFolder("Assets/_Game", "Resources");
            CreateFolder("Assets/_Game/Resources", "Maps");
            AssetDatabase.Refresh();

            ClearDir(TexDir);
            ClearDir(MatDir);
            ClearDir(LayerDir);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PrefabPath) != null) AssetDatabase.DeleteAsset(PrefabPath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TerrainPath) != null) AssetDatabase.DeleteAsset(TerrainPath);
        }

        static void ClearDir(string dir)
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p) && p != dir)
                    AssetDatabase.DeleteAsset(p);
            }
        }

        static void CreateFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        static Texture2D MakeTex(string file, Color a, Color b, float freq)
        {
            var path = TexDir + "/" + file;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var nse = Mathf.PerlinNoise(x * freq, y * freq);
                var n2 = Mathf.PerlinNoise(x * freq * 3.1f + 9f, y * freq * 3.1f);
                tex.SetPixel(x, y, Color.Lerp(a, b, nse * 0.65f + n2 * 0.2f));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.sRGBTexture = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D MakeCobble(string file)
        {
            var path = TexDir + "/" + file;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
            var grout = new Color(0.45f, 0.42f, 0.38f);
            var stone = new Color(0.72f, 0.68f, 0.60f);
            var stone2 = new Color(0.58f, 0.54f, 0.48f);
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var cell = 16;
                var lx = x % cell;
                var ly = y % cell;
                var edge = lx < 2 || ly < 2 || lx > cell - 3 || ly > cell - 3;
                var nse = Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
                tex.SetPixel(x, y, edge ? grout : Color.Lerp(stone, stone2, nse));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.sRGBTexture = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static TerrainLayer MakeLayer(string file, Texture2D tex, float tile)
        {
            var path = LayerDir + "/" + file;
            var layer = new TerrainLayer
            {
                diffuseTexture = tex,
                tileSize = new Vector2(tile, tile)
            };
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        static Material Lit(string name, Color color, float smooth, float metal, Color? emit = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal);
            if (emit.HasValue)
            {
                var e = emit.Value;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", e);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(mat, MatDir + "/" + name + ".mat");
            return mat;
        }

        static void MakeMaterials()
        {
            _sand = Lit("mat_sand", new Color(0.76f, 0.60f, 0.36f), 0.18f, 0f);
            _rock = Lit("mat_rock", new Color(0.40f, 0.32f, 0.26f), 0.22f, 0.05f);
            _cobble = Lit("mat_cobble", new Color(0.68f, 0.64f, 0.56f), 0.32f, 0.08f);
            _wood = Lit("mat_wood", new Color(0.38f, 0.24f, 0.14f), 0.28f, 0f);
            _cactus = Lit("mat_cactus", new Color(0.22f, 0.48f, 0.22f), 0.22f, 0f);
            _flower = Lit("mat_flower", new Color(0.92f, 0.32f, 0.55f), 0.35f, 0f);
            _roof = Lit("mat_roof", new Color(0.18f, 0.38f, 0.28f), 0.4f, 0.15f);
            _copper = Lit("mat_copper", new Color(0.78f, 0.42f, 0.18f), 0.55f, 0.75f);
            _plaster = Lit("mat_plaster", new Color(0.82f, 0.72f, 0.55f), 0.25f, 0f);
            _teal = Lit("mat_teal", GameTheme.Teal, 0.45f, 0.2f, GameTheme.Teal * 0.35f);
            _crimson = Lit("mat_crimson", GameTheme.Crimson, 0.45f, 0.2f, GameTheme.Crimson * 0.35f);
            _portal = Lit("mat_portal", new Color(0.55f, 0.22f, 0.85f), 0.5f, 0.1f, new Color(1.4f, 0.35f, 2.2f));
            _water = Lit("mat_water", new Color(0.15f, 0.72f, 0.78f), 0.85f, 0.05f, new Color(0.2f, 0.55f, 0.6f));
            _dark = Lit("mat_dark", new Color(0.06f, 0.05f, 0.04f), 0.1f, 0f);
            _blueCrystal = Lit("mat_crystal_blue", new Color(0.35f, 0.75f, 1f), 0.7f, 0.2f, new Color(0.4f, 1.1f, 1.6f));
            _amberCrystal = Lit("mat_crystal_amber", new Color(1f, 0.72f, 0.2f), 0.7f, 0.2f, new Color(1.5f, 0.8f, 0.2f));
            _camp = Lit("mat_camp", new Color(0.32f, 0.48f, 0.22f), 0.25f, 0f);

            var tShader = Shader.Find("Universal Render Pipeline/Terrain/Lit")
                          ?? Shader.Find("Universal Render Pipeline/Lit");
            if (tShader != null)
            {
                var tmat = new Material(tShader) { name = "FoldTerrainLit" };
                AssetDatabase.CreateAsset(tmat, TerrainLitPath);
            }
        }

        static TerrainData BuildTerrainData(TerrainLayer[] layers)
        {
            var data = new TerrainData
            {
                heightmapResolution = HeightRes,
                size = new Vector3(SizeX, SizeY, SizeZ)
            };
            data.SetDetailResolution(256, 16);
            data.terrainLayers = layers;

            var heights = new float[HeightRes, HeightRes];
            for (var z = 0; z < HeightRes; z++)
            for (var x = 0; x < HeightRes; x++)
            {
                var wx = TerrainX + x / (HeightRes - 1f) * SizeX;
                var wz = TerrainZ + z / (HeightRes - 1f) * SizeZ;
                heights[z, x] = SampleHeight(wx, wz);
            }
            data.SetHeights(0, 0, heights);

            data.alphamapResolution = AlphaRes;
            var maps = new float[AlphaRes, AlphaRes, 4];
            for (var z = 0; z < AlphaRes; z++)
            for (var x = 0; x < AlphaRes; x++)
            {
                var wx = TerrainX + x / (AlphaRes - 1f) * SizeX;
                var wz = TerrainZ + z / (AlphaRes - 1f) * SizeZ;
                Splat(wx, wz, out var s, out var r, out var c, out var k);
                maps[z, x, 0] = s;
                maps[z, x, 1] = r;
                maps[z, x, 2] = c;
                maps[z, x, 3] = k;
            }
            data.SetAlphamaps(0, 0, maps);
            return data;
        }

        static float SampleHeight(float x, float z)
        {
            var h = Play;
            h += Mathf.PerlinNoise(x * 0.055f + 8f, z * 0.055f) * 0.012f;
            h += Mathf.PerlinNoise(x * 0.16f, z * 0.16f + 4f) * 0.006f;

            var lane = Mathf.InverseLerp(6.2f, 3.0f, Mathf.Abs(z));
            var vpath = 0f;
            if (z < 2.5f)
                vpath = Mathf.InverseLerp(6.0f, 2.8f, Mathf.Abs(x)) * Mathf.InverseLerp(3.5f, 0.5f, z);
            var path = Mathf.Max(lane, vpath);
            if (Mathf.Abs(x) > 42f) path = 0f;
            h = Mathf.Lerp(h, Play + 0.003f, Mathf.Clamp01(path));

            var plaza = Mathf.InverseLerp(7.2f, 4.2f, Mathf.Sqrt(x * x + z * z));
            h = Mathf.Lerp(h, Play + 0.005f, plaza * 0.85f);

            h = Pit(h, x, z, -11f, -10.5f, 5.4f, 0.11f);
            h = Pit(h, x, z, 11f, -10.5f, 5.4f, 0.11f);

            if (z > 13.5f)
            {
                var t = Smooth01((z - 13.5f) / 32f);
                var drop = Mathf.Lerp(Play, 0.035f, t);
                if (z < 16.5f && Mathf.Abs(x) < 38f)
                    drop = Mathf.Lerp(Play, drop, Smooth01((z - 13.5f) / 3f));
                h = Mathf.Min(h, drop + Mathf.PerlinNoise(x * 0.1f, z * 0.1f) * 0.02f * t);
            }

            if (z < -22f)
            {
                var t = Smooth01((-22f - z) / 22f);
                h = Mathf.Lerp(h, 0.09f, t);
            }

            var side = Mathf.Max(0f, Mathf.Abs(x) - 43f);
            if (side > 0f)
            {
                var t = Smooth01(side / 11f);
                h += t * 0.20f;
                h += Mathf.PerlinNoise(x * 0.18f, z * 0.14f) * 0.045f * t;
            }

            h += Mound(x, z, -38f, 0f, 9f, 0.025f);
            h += Mound(x, z, 38f, 0f, 9f, 0.025f);
            h += Outcrop(x, z, -20f, -8f, 4.2f, 0.038f);
            h += Outcrop(x, z, 20f, -8f, 4.2f, 0.038f);
            h += Outcrop(x, z, -7f, -17f, 3.2f, 0.03f);
            h += Outcrop(x, z, 7f, -17f, 3.2f, 0.03f);
            h += Outcrop(x, z, -16f, 8.5f, 3.6f, 0.04f);
            h += Outcrop(x, z, 16f, 8.5f, 3.6f, 0.04f);
            h += Outcrop(x, z, -28f, -14f, 3.5f, 0.028f);
            h += Outcrop(x, z, 28f, -14f, 3.5f, 0.028f);

            return Mathf.Clamp01(h);
        }

        static void Splat(float x, float z, out float sand, out float rock, out float cobble, out float canyon)
        {
            sand = 1f;
            rock = 0f;
            cobble = 0f;
            canyon = 0f;

            var onLane = Mathf.Abs(z) < 4.6f && Mathf.Abs(x) < 42f;
            var onVert = Mathf.Abs(x) < 4.6f && z < 2.2f && z > -24f;
            var onPlaza = x * x + z * z < 6.5f * 6.5f;
            if (onLane || onVert || onPlaza)
            {
                cobble = 0.92f;
                sand = 0.08f;
            }

            var pitL = Dist(x, z, -11f, -10.5f);
            var pitR = Dist(x, z, 11f, -10.5f);
            if (pitL < 5.8f || pitR < 5.8f)
            {
                var d = Mathf.Min(pitL, pitR);
                var rim = Mathf.InverseLerp(5.8f, 4.2f, d);
                var hole = Mathf.InverseLerp(4.4f, 2.2f, d);
                rock = Mathf.Max(rock, rim * 0.85f);
                canyon = Mathf.Max(canyon, hole);
                cobble *= 1f - hole;
                sand = Mathf.Max(0f, 1f - rock - cobble - canyon);
            }

            if (z > 15f)
            {
                var t = Smooth01((z - 15f) / 10f);
                canyon = Mathf.Max(canyon, t);
                sand *= 1f - t;
                cobble *= 1f - t;
            }

            if (Mathf.Abs(x) > 42f || OutcropMask(x, z) > 0.35f)
            {
                rock = Mathf.Max(rock, 0.7f);
                sand *= 0.3f;
            }

            var sum = sand + rock + cobble + canyon;
            if (sum < 0.001f) { sand = 1f; return; }
            sand /= sum;
            rock /= sum;
            cobble /= sum;
            canyon /= sum;
        }

        static float Pit(float h, float x, float z, float px, float pz, float r, float bottom)
        {
            var d = Dist(x, z, px, pz);
            if (d >= r) return h;
            var t = Smooth01(1f - d / r);
            return Mathf.Lerp(h, bottom, t * t);
        }

        static float Mound(float x, float z, float px, float pz, float r, float amp)
        {
            var t = 1f - Mathf.Clamp01(Dist(x, z, px, pz) / r);
            return t * t * amp;
        }

        static float Outcrop(float x, float z, float px, float pz, float r, float amp)
        {
            var d = Dist(x, z, px, pz);
            if (d >= r) return 0f;
            var n = Mathf.PerlinNoise(x * 0.35f + px, z * 0.35f);
            return (1f - d / r) * amp * (0.55f + n * 0.7f);
        }

        static float OutcropMask(float x, float z)
        {
            var m = 0f;
            m = Mathf.Max(m, 1f - Dist(x, z, -20f, -8f) / 4.2f);
            m = Mathf.Max(m, 1f - Dist(x, z, 20f, -8f) / 4.2f);
            m = Mathf.Max(m, 1f - Dist(x, z, -16f, 8.5f) / 3.6f);
            m = Mathf.Max(m, 1f - Dist(x, z, 16f, 8.5f) / 3.6f);
            return Mathf.Clamp01(m);
        }

        static float Dist(float x, float z, float px, float pz)
        {
            var dx = x - px;
            var dz = z - pz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void PlaceGameplay(Transform parent)
        {
            Marker(parent, "DawnSpawn", new Vector3(-FoldMapBuilder.HalfLength, 1.35f, 0f));
            Marker(parent, "DuskSpawn", new Vector3(FoldMapBuilder.HalfLength, 1.35f, 0f));

            Prim(parent, PrimitiveType.Cylinder, new Vector3(-16f, 1.6f, 0f), new Vector3(1.6f, 1.6f, 1.6f), _teal, "TurretDawn", true);
            Prim(parent, PrimitiveType.Cylinder, new Vector3(16f, 1.6f, 0f), new Vector3(1.6f, 1.6f, 1.6f), _crimson, "TurretDusk", true);

            var cd = Prim(parent, PrimitiveType.Cube, new Vector3(-FoldMapBuilder.HalfLength - 3.5f, 2.2f, 0f), new Vector3(1.4f, 4.2f, 1.4f), _teal, "CrystalDawn", true);
            cd.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var ck = Prim(parent, PrimitiveType.Cube, new Vector3(FoldMapBuilder.HalfLength + 3.5f, 2.2f, 0f), new Vector3(1.4f, 4.2f, 1.4f), _crimson, "CrystalDusk", true);
            ck.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            Prim(parent, PrimitiveType.Sphere, new Vector3(-12f, 0.55f, 13f), Vector3.one * 1.5f, _camp, "CampNL", true);
            Prim(parent, PrimitiveType.Sphere, new Vector3(12f, 0.55f, 13f), Vector3.one * 1.5f, _camp, "CampNR", true);
            Prim(parent, PrimitiveType.Sphere, new Vector3(-12f, 0.55f, -13f), Vector3.one * 1.5f, _camp, "CampSL", true);
            Prim(parent, PrimitiveType.Sphere, new Vector3(12f, 0.55f, -13f), Vector3.one * 1.5f, _camp, "CampSR", true);
        }

        static void PlaceLaneDressing(Transform parent)
        {
            Prim(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(11.5f, 0.06f, 11.5f), _cobble, "PlazaRing", false);
            Prim(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(4.2f, 0.05f, 4.2f), _rock, "PlazaStar", false);

            var rail = Group(parent, "Railings");
            for (var i = 0; i <= 26; i++)
            {
                var x = Mathf.Lerp(-39f, 39f, i / 26f);
                Prim(rail, PrimitiveType.Cube, new Vector3(x, 0.55f, 4.6f), new Vector3(2.6f, 0.12f, 0.12f), _wood, "RailN_" + i, false);
                if (i % 2 == 0)
                    Prim(rail, PrimitiveType.Cube, new Vector3(x, 0.35f, 4.6f), new Vector3(0.12f, 0.7f, 0.12f), _wood, "PostN_" + i, false);
            }
            for (var i = 0; i <= 10; i++)
            {
                var z = Mathf.Lerp(-1f, -21f, i / 10f);
                Prim(rail, PrimitiveType.Cube, new Vector3(-4.5f, 0.55f, z), new Vector3(0.12f, 0.12f, 1.8f), _wood, "RailWL_" + i, false);
                Prim(rail, PrimitiveType.Cube, new Vector3(4.5f, 0.55f, z), new Vector3(0.12f, 0.12f, 1.8f), _wood, "RailWR_" + i, false);
            }
        }

        static void PlaceBases(Transform parent)
        {
            Prim(parent, PrimitiveType.Cylinder, new Vector3(-38f, 0.12f, 0f), new Vector3(8.2f, 0.12f, 8.2f), _cobble, "DawnPad", false);
            Prim(parent, PrimitiveType.Cylinder, new Vector3(38f, 0.12f, 0f), new Vector3(8.2f, 0.12f, 8.2f), _cobble, "DuskPad", false);
            Prim(parent, PrimitiveType.Cube, new Vector3(-38f, 1.2f, -4.2f), new Vector3(3.2f, 2.4f, 1.2f), _plaster, "DawnShop", true);
            Prim(parent, PrimitiveType.Cube, new Vector3(38f, 1.2f, -4.2f), new Vector3(3.2f, 2.4f, 1.2f), _plaster, "DuskShop", true);

            Prim(parent, PrimitiveType.Cylinder, new Vector3(-42.5f, 0.2f, 0f), new Vector3(6.4f, 0.18f, 6.4f), _rock, "PortalRing", false);
            Prim(parent, PrimitiveType.Cylinder, new Vector3(-42.5f, 1.1f, 0f), new Vector3(4.2f, 0.08f, 4.2f), _portal, "PortalGlow", false);

            Building(parent, new Vector3(-50f, 0f, 6f), 6.5f, 7.5f, 5.5f, "DawnHouseA");
            Building(parent, new Vector3(-49f, 0f, -7.5f), 5f, 5.5f, 4.5f, "DawnHouseB");
            Prim(parent, PrimitiveType.Cube, new Vector3(-47f, 2.2f, 0f), new Vector3(1.2f, 4.4f, 8f), _plaster, "DawnWall", false);

            Prim(parent, PrimitiveType.Cylinder, new Vector3(48f, 3.2f, 3.5f), new Vector3(3.4f, 3.2f, 3.4f), _plaster, "DuskTower", false);
            Prim(parent, PrimitiveType.Sphere, new Vector3(48f, 6.6f, 3.5f), new Vector3(3.8f, 2.4f, 3.8f), _copper, "DuskDome", false);
            Prim(parent, PrimitiveType.Cube, new Vector3(47f, 1.1f, -3f), new Vector3(4.5f, 2.2f, 3.2f), _wood, "DuskTent", false);
            Prim(parent, PrimitiveType.Cube, new Vector3(46f, 1.0f, 8f), new Vector3(0.35f, 2.0f, 3.6f), _wood, "DuskBarricadeA", false);
            Prim(parent, PrimitiveType.Cube, new Vector3(50f, 1.0f, -6f), new Vector3(3.2f, 1.8f, 0.35f), _wood, "DuskBarricadeB", false);
        }

        static void Building(Transform parent, Vector3 pos, float w, float h, float d, string name)
        {
            Prim(parent, PrimitiveType.Cube, pos + new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), _plaster, name, false);
            Prim(parent, PrimitiveType.Cube, pos + new Vector3(0f, h + 0.45f, 0f), new Vector3(w + 0.8f, 0.7f, d + 0.8f), _roof, name + "Roof", false);
            Prim(parent, PrimitiveType.Cube, pos + new Vector3(0f, h + 1.3f, 0f), new Vector3(w * 0.55f, 0.55f, d * 0.55f), _roof, name + "Roof2", false);
        }

        static void PlacePits(Transform parent)
        {
            PitDress(parent, new Vector3(-11f, 0f, -10.5f), "PitL", _blueCrystal);
            PitDress(parent, new Vector3(11f, 0f, -10.5f), "PitR", _amberCrystal);
        }

        static void PitDress(Transform parent, Vector3 c, string name, Material crystal)
        {
            Prim(parent, PrimitiveType.Cylinder, c + new Vector3(0f, -1.6f, 0f), new Vector3(9.2f, 1.8f, 9.2f), _dark, name + "Well", false);
            for (var i = 0; i < 8; i++)
            {
                var a = i / 8f * Mathf.PI * 2f;
                var p = c + new Vector3(Mathf.Cos(a) * 5.1f, 0.45f, Mathf.Sin(a) * 5.1f);
                Prim(parent, PrimitiveType.Cube, p, new Vector3(1.6f, 0.7f, 1.1f), _rock, name + "Rim" + i, false)
                    .transform.rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            }
            var cr = Prim(parent, PrimitiveType.Cube, c + new Vector3(3.8f, 0.9f, 2.2f), new Vector3(0.9f, 1.8f, 0.9f), crystal, name + "Crystal", false);
            cr.transform.rotation = Quaternion.Euler(12f, 35f, 18f);
        }

        static void PlaceStairs(Transform parent)
        {
            Stairs(parent, new Vector3(-20f, 0f, -22.5f), "StairsL");
            Stairs(parent, new Vector3(20f, 0f, -22.5f), "StairsR");
        }

        static void Stairs(Transform parent, Vector3 origin, string name)
        {
            for (var i = 0; i < 8; i++)
            {
                Prim(parent, PrimitiveType.Cube,
                    origin + new Vector3(0f, -0.28f * i, -1.05f * i),
                    new Vector3(5.5f, 0.28f, 1.15f), _cobble, name + "_" + i, false);
            }
        }

        static void PlaceFence(Transform parent)
        {
            var fence = Group(parent, "NorthFence");
            for (var i = 0; i <= 28; i++)
            {
                var x = Mathf.Lerp(-40f, 40f, i / 28f);
                Prim(fence, PrimitiveType.Cube, new Vector3(x, 0.85f, 13.4f), new Vector3(0.16f, 1.7f, 0.16f), _wood, "FPost_" + i, false);
                if (i < 28)
                    Prim(fence, PrimitiveType.Cube, new Vector3(x + 1.4f, 1.15f, 13.4f), new Vector3(2.7f, 0.12f, 0.1f), _wood, "FRail_" + i, false);
            }
        }

        static void PlaceFoliage(Transform parent)
        {
            var rng = new System.Random(20260828);
            var cacti = Group(parent, "Cacti");
            var flowers = Group(parent, "Flowers");
            var rocks = Group(parent, "Rocks");

            Vector3[] cactusSpots =
            {
                new Vector3(-8.2f, 0f, -8.4f), new Vector3(-6.4f, 0f, -10.8f), new Vector3(-10.5f, 0f, -7.2f),
                new Vector3(8.2f, 0f, -8.4f), new Vector3(6.4f, 0f, -10.8f), new Vector3(10.5f, 0f, -7.2f),
                new Vector3(-1.6f, 0f, -15.4f), new Vector3(2.1f, 0f, -16.6f), new Vector3(0.2f, 0f, -18.5f),
                new Vector3(-18.4f, 0f, -12.2f), new Vector3(-16.1f, 0f, -15.0f), new Vector3(-20.6f, 0f, -9.8f),
                new Vector3(18.4f, 0f, -12.2f), new Vector3(16.1f, 0f, -15.0f), new Vector3(20.6f, 0f, -9.8f),
                new Vector3(-13.8f, 0f, -6.2f), new Vector3(13.8f, 0f, -6.2f),
                new Vector3(-22.5f, 0f, -16.4f), new Vector3(22.5f, 0f, -16.4f),
                new Vector3(-5.4f, 0f, -20.2f), new Vector3(5.4f, 0f, -20.2f),
                new Vector3(-25f, 0f, -8f), new Vector3(25f, 0f, -8f)
            };
            for (var i = 0; i < cactusSpots.Length; i++)
                Cactus(cacti, cactusSpots[i], 0.85f + (float)rng.NextDouble() * 0.45f, rng, i);

            Vector3[] flowerSpots =
            {
                new Vector3(-9.5f, 0.35f, -6.5f), new Vector3(9.5f, 0.35f, -6.5f),
                new Vector3(-17f, 0.35f, -10f), new Vector3(17f, 0.35f, -10f),
                new Vector3(-3f, 0.35f, -17.5f), new Vector3(4f, 0.35f, -18f),
                new Vector3(-21f, 0.35f, -14f), new Vector3(21f, 0.35f, -14f),
                new Vector3(-14f, 0.35f, -18f), new Vector3(14f, 0.35f, -18f)
            };
            foreach (var p in flowerSpots)
                Prim(flowers, PrimitiveType.Sphere, p, new Vector3(1.6f, 0.7f, 1.6f), _flower, "Flower", false);

            Vector3[] rockSpots =
            {
                new Vector3(-19.5f, 0.5f, -7.5f), new Vector3(19.5f, 0.5f, -7.5f),
                new Vector3(-6.5f, 0.4f, -17.5f), new Vector3(6.5f, 0.4f, -17.5f),
                new Vector3(-15.5f, 0.55f, 8.2f), new Vector3(15.5f, 0.55f, 8.2f),
                new Vector3(-27f, 0.6f, -12f), new Vector3(27f, 0.6f, -12f)
            };
            foreach (var p in rockSpots)
            {
                var go = Prim(rocks, PrimitiveType.Cube, p, new Vector3(2.4f, 1.2f, 1.8f), _rock, "Rock", false);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 8f);
            }
        }

        static void Cactus(Transform parent, Vector3 pos, float s, System.Random rng, int id)
        {
            var root = Group(parent, "Cactus_" + id);
            root.position = pos;
            Prim(root, PrimitiveType.Cylinder, new Vector3(0f, 1.15f * s, 0f), new Vector3(0.38f * s, 1.15f * s, 0.38f * s), _cactus, "Stem", false);
            var arms = 2 + rng.Next(2);
            for (var i = 0; i < arms; i++)
            {
                var side = i % 2 == 0 ? 1f : -1f;
                var y = (0.7f + (float)rng.NextDouble() * 0.7f) * s;
                Prim(root, PrimitiveType.Cylinder, new Vector3(side * 0.42f * s, y, 0f),
                    new Vector3(0.55f * s, 0.16f * s, 0.16f * s), _cactus, "ArmH" + i, false);
                Prim(root, PrimitiveType.Cylinder, new Vector3(side * 0.68f * s, y + 0.45f * s, 0f),
                    new Vector3(0.16f * s, 0.45f * s, 0.16f * s), _cactus, "ArmV" + i, false);
            }
        }

        static void PlaceBrushes(Transform parent)
        {
            // Стартовые зоны у кластеров кактусов (юг). Северных кустов нет — подвинете в Prefab Mode.
            Brush(parent, new Vector3(-8f, 0.4f, -9f), 3.6f, "Brush_SL_Pit");
            Brush(parent, new Vector3(8f, 0.4f, -9f), 3.6f, "Brush_SR_Pit");
            Brush(parent, new Vector3(0f, 0.4f, -16.5f), 3.8f, "Brush_S");
            Brush(parent, new Vector3(-18f, 0.4f, -13f), 3.5f, "Brush_SW");
            Brush(parent, new Vector3(18f, 0.4f, -13f), 3.5f, "Brush_SE");
            Brush(parent, new Vector3(-14f, 0.4f, -6.2f), 3.2f, "Brush_LaneL");
            Brush(parent, new Vector3(14f, 0.4f, -6.2f), 3.2f, "Brush_LaneR");
        }

        static void Brush(Transform parent, Vector3 pos, float radius, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var zone = go.AddComponent<BrushZone>();
            zone.Radius = radius;
        }

        static void PlaceCanyonVista(Transform parent)
        {
            var vista = Group(parent, "CanyonVista");
            Prim(vista, PrimitiveType.Cube, new Vector3(0f, -10.4f, 48f), new Vector3(170f, 0.5f, 42f), _water, "River", false);
            Pavilion(vista, new Vector3(-22f, -2.5f, 38f), "PavL");
            Pavilion(vista, new Vector3(18f, -1.8f, 42f), "PavR");
            Prim(vista, PrimitiveType.Cylinder, new Vector3(-8f, 2f, 52f), new Vector3(3.2f, 8f, 3.2f), _plaster, "FarTowerA", false);
            Prim(vista, PrimitiveType.Cube, new Vector3(-8f, 10.4f, 52f), new Vector3(4.4f, 1.1f, 4.4f), _roof, "FarTowerARoof", false);
            Prim(vista, PrimitiveType.Cylinder, new Vector3(26f, 1.2f, 58f), new Vector3(2.6f, 7.2f, 2.6f), _plaster, "FarTowerB", false);
            Prim(vista, PrimitiveType.Sphere, new Vector3(26f, 8.6f, 58f), new Vector3(3.2f, 2.2f, 3.2f), _copper, "FarTowerBDome", false);
        }

        static void Pavilion(Transform parent, Vector3 pos, string name)
        {
            Prim(parent, PrimitiveType.Cylinder, pos, new Vector3(4.5f, 0.2f, 4.5f), _cobble, name + "Floor", false);
            for (var i = 0; i < 6; i++)
            {
                var a = i / 6f * Mathf.PI * 2f;
                Prim(parent, PrimitiveType.Cylinder,
                    pos + new Vector3(Mathf.Cos(a) * 1.7f, 1.6f, Mathf.Sin(a) * 1.7f),
                    new Vector3(0.22f, 1.6f, 0.22f), _wood, name + "Col" + i, false);
            }
            Prim(parent, PrimitiveType.Cylinder, pos + new Vector3(0f, 3.4f, 0f), new Vector3(5.2f, 0.25f, 5.2f), _roof, name + "Roof", false);
        }

        static void Marker(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
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
                if (col != null) Object.DestroyImmediate(col);
            }
            return go;
        }
    }
}
