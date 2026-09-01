using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ashfold.Editor
{
    /// <summary>
    /// Импорт FBX (ось/масштаб), материалы из PNG, префабы турели/кристалла/крипа,
    /// подстановка в FoldMap. Меню: Ashfold → Setup Map Models.
    /// </summary>
    public static class MapModelSetup
    {
        const string ModelsRoot = "Assets/_Game/Art/Map/Models";
        const string MatDir = "Assets/_Game/Art/Map/Materials";
        const string AnimDir = "Assets/_Game/Art/Map/Anim";
        const string PartsDir = "Assets/_Game/Resources/Maps/Parts";
        const string UnitsDir = "Assets/_Game/Resources/Units";
        const string MapPrefab = "Assets/_Game/Resources/Maps/FoldMap.prefab";
        const string TurretPrefab = PartsDir + "/Turret.prefab";
        const string CrystalPrefab = PartsDir + "/Crystal.prefab";
        const string MinionPrefab = UnitsDir + "/MinionVisual.prefab";
        const string CaptainPrefab = UnitsDir + "/CaptainVisual.prefab";
        const string ControllerPath = AnimDir + "/Minion.controller";
        const string CaptainControllerPath = AnimDir + "/Captain.controller";

        const string TurelFbx = ModelsRoot + "/turel_1/turel_1.fbx";
        const string CitadelFbx = ModelsRoot + "/citadel_1/citadel_1.fbx";
        const string KristallFbx = ModelsRoot + "/kristall_1/kristall_1.fbx";
        const string KripFbx = ModelsRoot + "/krip_1/krip_1.fbx";
        const string Krip2Fbx = ModelsRoot + "/krip_2/krip_2.fbx";

        const float TurretHeight = 3.2f;
        const float CitadelHeight = 2.35f;
        const float CrystalHeight = 3.2f;
        const float MinionLocalHeight = 2f;

        [MenuItem("Ashfold/Setup Map Models")]
        public static void SetupFromMenu()
        {
            var log = Setup();
            EditorUtility.DisplayDialog("Ashfold", log, "OK");
        }

        public static void SetupFromCommandLine()
        {
            Debug.Log("[Ashfold] " + Setup());
        }

        public static GameObject PlaceTurret(Transform parent, Vector3 pos, bool dawn, string name)
        {
            return PlacePart(TurretPrefab, parent, pos, Yaw(dawn), name);
        }

        public static GameObject PlaceCrystal(Transform parent, Vector3 pos, bool dawn, string name)
        {
            return PlacePart(CrystalPrefab, parent, pos, Yaw(dawn), name);
        }

        public static string Setup()
        {
            EnsureFolders();
            ConfigureImporters();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var kripMat = MakeTextured("mat_krip_1", ModelsRoot + "/krip_1/krip_1.png", 0.38f, 0.12f, null);
            var krip2Mat = MakeTextured("mat_krip_2", ModelsRoot + "/krip_2/krip_2.png", 0.38f, 0.12f, null);

            var controller = BuildCreepController(KripFbx, ControllerPath);
            var captainCtrl = BuildCreepController(Krip2Fbx, CaptainControllerPath);
            var minion = BuildCreepPrefab(KripFbx, kripMat, controller, MinionPrefab, "Krip");
            var captain = BuildCreepPrefab(Krip2Fbx, krip2Mat, captainCtrl, CaptainPrefab, "Krip");
            if (!FoldMapHasArt())
            {
                var turelMat = MakeTextured("mat_turel_1", ModelsRoot + "/turel_1/turel_1.png", 0.42f, 0.35f, null);
                var citadelMat = MakeTextured("mat_citadel_1", ModelsRoot + "/citadel_1/citadel_1.png", 0.32f, 0.15f, null);
                var kristallMat = MakeTextured("mat_kristall_1", ModelsRoot + "/kristall_1/kristall_1.png", 0.72f, 0.08f, new Color(0.18f, 0.28f, 0.45f));
                var turret = BuildTurretPrefab(turelMat);
                var crystal = BuildCrystalPrefab(citadelMat, kristallMat);
                PatchFoldMap(turret, crystal);
            }

            var mBounds = Describe(minion);
            var capBounds = Describe(captain);
            return "Модели настроены.\n\n" +
                   "Крип: " + mBounds + "\n" +
                   "Капитан: " + capBounds + "\n\n" +
                   "Префабы:\n" + MinionPrefab + "\n" + CaptainPrefab;
        }

        static Quaternion Yaw(bool dawn) => Quaternion.Euler(0f, dawn ? 90f : -90f, 0f);

        static GameObject PlacePart(string path, Transform parent, Vector3 pos, Quaternion rot, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one;
            return go;
        }

        static void EnsureFolders()
        {
            Directory.CreateDirectory(Abs("Assets/_Game/Resources/Maps/Parts"));
            Directory.CreateDirectory(Abs("Assets/_Game/Resources/Units"));
            Directory.CreateDirectory(Abs("Assets/_Game/Art/Map/Anim"));
            Directory.CreateDirectory(Abs("Assets/_Game/Art/Map/Materials"));
        }

        static string Abs(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        static void ConfigureImporters()
        {
            ConfigureStatic(TurelFbx);
            ConfigureStatic(CitadelFbx);
            ConfigureStatic(KristallFbx);
            ConfigureKrip(KripFbx);
            if (File.Exists(Abs(Krip2Fbx)))
                ConfigureKrip(Krip2Fbx);
        }

        static void ConfigureStatic(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException("FBX не найден: " + path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.bakeAxisConversion = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
        }

        static void ConfigureKrip(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException("FBX не найден: " + path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.useFileScale = false;
            importer.globalScale = 0.01f;
            importer.bakeAxisConversion = false;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                foreach (var clip in clips)
                {
                    var n = clip.name ?? "";
                    var run = n.IndexOf("run", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    var walk = n.IndexOf("walk", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    clip.loopTime = run || walk;
                    clip.loopPose = run || walk;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalPositionY = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        static Material MakeTextured(string name, string texPath, float smooth, float metal, Color? emit)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            var path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
                mat.shader = shader;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal);
            if (emit.HasValue)
            {
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emit.Value);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static AnimatorController BuildCreepController(string fbx, string path)
        {
            var clips = LoadClips(fbx);
            var run = FindClip(clips, "run", "running");
            var attack = FindClip(clips, "kick", "boxing", "cast", "attack", "spell", "soell");
            if (run == null && clips.Length > 0)
                run = clips[0];

            if (File.Exists(Abs(path)))
                AssetDatabase.DeleteAsset(path);

            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;
            var runState = sm.AddState("Run");
            runState.motion = run;
            sm.defaultState = runState;
            if (attack != null)
            {
                var atk = sm.AddState("Attack");
                atk.motion = attack;
                atk.tag = "Attack";
                var toAtk = sm.AddAnyStateTransition(atk);
                toAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                toAtk.hasExitTime = false;
                toAtk.duration = 0.06f;
                toAtk.canTransitionToSelf = false;
                var back = atk.AddTransition(runState);
                back.hasExitTime = true;
                back.exitTime = 0.88f;
                back.hasFixedDuration = true;
                back.duration = 0.1f;
            }
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static AnimationClip[] LoadClips(string fbx)
        {
            var list = new System.Collections.Generic.List<AnimationClip>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    list.Add(clip);
            }
            return list.ToArray();
        }

        static AnimationClip FindClip(AnimationClip[] clips, params string[] keys)
        {
            foreach (var key in keys)
            {
                foreach (var clip in clips)
                {
                    if (clip.name.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return clip;
                }
            }
            return null;
        }

        static GameObject BuildTurretPrefab(Material mat)
        {
            var root = new GameObject("Turret");
            try
            {
                var vis = InstantiateModel(TurelFbx, root.transform, "Visual");
                StripAnimators(vis);
                ApplyMat(vis, mat);
                UprightFitSit(vis.transform, TurretHeight);
                FitCollider(root, true);
                SavePrefab(root, TurretPrefab);
                return AssetDatabase.LoadAssetAtPath<GameObject>(TurretPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static GameObject BuildCrystalPrefab(Material citadelMat, Material kristallMat)
        {
            var root = new GameObject("Crystal");
            try
            {
                var citadel = InstantiateModel(CitadelFbx, root.transform, "Base");
                var crystal = InstantiateModel(KristallFbx, root.transform, "Gem");
                StripAnimators(citadel);
                StripAnimators(crystal);
                ApplyMat(citadel, citadelMat);
                ApplyMat(crystal, kristallMat);
                UprightFitSit(citadel.transform, CitadelHeight);
                UprightFitSit(crystal.transform, CrystalHeight);
                NestCrystal(citadel.transform, crystal.transform);
                if (root.GetComponent<CrystalGemMotion>() == null)
                {
                    var motion = root.AddComponent<CrystalGemMotion>();
                    motion.Gem = crystal.transform;
                }
                FitCollider(root, false);
                SavePrefab(root, CrystalPrefab);
                return AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPrefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static GameObject BuildCreepPrefab(string fbx, Material mat, AnimatorController controller, string prefabPath, string childName)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var vis = InstantiateModel(fbx, root.transform, childName);
                ApplyMat(vis, mat);
                UprightFitSit(vis.transform, MinionLocalHeight);
                var anim = vis.GetComponent<Animator>() ?? vis.GetComponentInChildren<Animator>();
                if (anim == null)
                    anim = vis.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                SavePrefab(root, prefabPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static GameObject InstantiateModel(string fbxPath, Transform parent, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (asset == null)
                throw new FileNotFoundException("Не загрузился FBX: " + fbxPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            return go;
        }

        static void StripAnimators(GameObject go)
        {
            foreach (var a in go.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(a);
        }

        static void ApplyMat(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var arr = r.sharedMaterials;
                for (var i = 0; i < arr.Length; i++)
                    arr[i] = mat;
                r.sharedMaterials = arr;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        static Bounds RendererBounds(Transform t)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return new Bounds(t.position, Vector3.zero);
            var b = rends[0].bounds;
            for (var i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            return b;
        }

        static void UprightFitSit(Transform t, float targetHeight)
        {
            t.localPosition = Vector3.zero;
            var b = RendererBounds(t);
            if (b.size.sqrMagnitude < 1e-8f)
                return;
            var maxXZ = Mathf.Max(b.size.x, b.size.z);
            if (b.size.y < maxXZ * 0.65f)
            {
                t.localRotation = Quaternion.Euler(-90f, 0f, 0f) * t.localRotation;
                b = RendererBounds(t);
            }
            var h = Mathf.Max(b.size.y, 0.0001f);
            t.localScale *= targetHeight / h;
            SitOnParentGround(t);
        }

        static void SitOnParentGround(Transform t)
        {
            var b = RendererBounds(t);
            var origin = t.parent != null ? t.parent.position : Vector3.zero;
            t.position += new Vector3(origin.x - b.center.x, origin.y - b.min.y, origin.z - b.center.z);
        }

        static void NestCrystal(Transform citadel, Transform crystal)
        {
            var baseB = RendererBounds(citadel);
            var gemB = RendererBounds(crystal);
            var baseFoot = Mathf.Min(baseB.size.x, baseB.size.z);
            var gemFoot = Mathf.Max(gemB.size.x, gemB.size.z, 0.0001f);
            var s = Mathf.Clamp((baseFoot * 0.52f) / gemFoot, 0.35f, 1.15f);
            crystal.localScale *= s;
            gemB = RendererBounds(crystal);
            var targetMinY = baseB.min.y + baseB.size.y * 0.18f;
            crystal.position += new Vector3(
                baseB.center.x - gemB.center.x,
                targetMinY - gemB.min.y,
                baseB.center.z - gemB.center.z);
        }

        static void FitCollider(GameObject go, bool capsule)
        {
            foreach (var c in go.GetComponents<Collider>())
                Object.DestroyImmediate(c);
            var b = RendererBounds(go.transform);
            var center = go.transform.InverseTransformPoint(b.center);
            var lossy = go.transform.lossyScale;
            var size = new Vector3(
                b.size.x / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f),
                b.size.y / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f),
                b.size.z / Mathf.Max(Mathf.Abs(lossy.z), 0.0001f));
            if (capsule)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.center = center;
                cap.height = size.y;
                cap.radius = Mathf.Max(size.x, size.z) * 0.42f;
            }
            else
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = center;
                box.size = size;
            }
        }

        static void SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Abs(path)) ?? "");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        static bool FoldMapHasArt()
        {
            if (!File.Exists(Abs(MapPrefab)))
                return false;
            var root = PrefabUtility.LoadPrefabContents(MapPrefab);
            try
            {
                var crystal = FindNamed(root.transform, "CrystalDawn");
                return crystal != null && FindNamed(crystal.transform, "Gem") != null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void PatchFoldMap(GameObject turretPrefab, GameObject crystalPrefab)
        {
            if (!File.Exists(Abs(MapPrefab)))
            {
                Debug.LogWarning("[Ashfold] FoldMap.prefab не найден, пропускаю подстановку.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(MapPrefab);
            try
            {
                var crystal = FindNamed(root.transform, "CrystalDawn");
                if (crystal != null && FindNamed(crystal.transform, "Gem") != null)
                {
                    Debug.Log("[Ashfold] FoldMap уже с моделями — турели/кристаллы не трогаю.");
                    return;
                }
                var auth = root.GetComponent<FoldMapAuthoring>();
                var gameplayGo = FindNamed(root.transform, "Gameplay");
                var gameplay = gameplayGo != null ? gameplayGo.transform : root.transform;
                auth.TurretDawn = ReplaceNamed(gameplay, "TurretDawn", turretPrefab, new Vector3(-16f, 0f, 0f), Yaw(true));
                auth.TurretDusk = ReplaceNamed(gameplay, "TurretDusk", turretPrefab, new Vector3(16f, 0f, 0f), Yaw(false));
                auth.CrystalDawn = ReplaceNamed(gameplay, "CrystalDawn", crystalPrefab,
                    new Vector3(-FoldMapBuilder.HalfLength - 3.5f, 0f, 0f), Yaw(true));
                auth.CrystalDusk = ReplaceNamed(gameplay, "CrystalDusk", crystalPrefab,
                    new Vector3(FoldMapBuilder.HalfLength + 3.5f, 0f, 0f), Yaw(false));
                EditorUtility.SetDirty(auth);
                PrefabUtility.SaveAsPrefabAsset(root, MapPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject ReplaceNamed(Transform parent, string name, GameObject prefab, Vector3 pos, Quaternion rot)
        {
            var old = FindNamed(parent, name);
            var idx = old != null ? old.transform.GetSiblingIndex() : parent.childCount;
            if (old != null)
                Object.DestroyImmediate(old);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetSiblingIndex(idx);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one;
            return go;
        }

        static GameObject FindNamed(Transform root, string name)
        {
            if (root.name == name)
                return root.gameObject;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t.gameObject;
            }
            return null;
        }

        static string Describe(GameObject prefab)
        {
            if (prefab == null)
                return "нет";
            var tmp = Object.Instantiate(prefab);
            try
            {
                var b = RendererBounds(tmp.transform);
                return $"h={b.size.y:0.00}  xz={b.size.x:0.00}×{b.size.z:0.00}";
            }
            finally
            {
                Object.DestroyImmediate(tmp);
            }
        }
    }
}
