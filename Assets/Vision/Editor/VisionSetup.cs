using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Vision.Player;
using Vision.Rendering;
using Vision.World;

namespace Vision.EditorTools
{
    /// <summary>
    /// One-click (or batch-mode) project setup: materials, the VisionSandbox scene and a Windows build.
    /// Batch: unity run . -- -executeMethod Vision.EditorTools.VisionSetup.CreateSandbox
    /// </summary>
    public static class VisionSetup
    {
        const string Root = "Assets/Vision";
        const string ScenePath = Root + "/Scenes/VisionSandbox.unity";
        const string BuildPath = "Builds/Windows/VisionSandbox.exe";

        /// <summary>
        /// Project identity and target platform: a Windows desktop game (64-bit, Mono).
        /// Batch: unity run . -- -executeMethod Vision.EditorTools.VisionSetup.ConfigureProject
        /// </summary>
        [MenuItem("Vision/Configure Project")]
        public static void ConfigureProject()
        {
            PlayerSettings.companyName = "zchris07";
            PlayerSettings.productName = "Manhunt";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.zchris07.manhunt");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Vision] Project configured: {PlayerSettings.companyName} / {PlayerSettings.productName}, target {EditorUserBuildSettings.activeBuildTarget}");
        }

        /// <summary>
        /// Bakes the whole level: materials, prop meshes and prefabs, the prop library, a mesh asset for
        /// every generated mesh, and the VisionSandbox scene with the level laid out as ordinary objects.
        /// Existing assets are overwritten in place.
        /// </summary>
        [MenuItem("Vision/Bake Level and Scene")]
        public static void CreateSandbox()
        {
            ConfigureShadows();
            Material lowPoly = MakeMaterial("LowPoly", 0f, false);
            Material entity = MakeMaterial("LowPolyEntity", 0f, true);
            Material glow = MakeMaterial("LowPolyGlow", 0.6f, false);
            PropLibrary library = LevelBaker.BakeProps(lowPoly, entity, glow);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Creating a scene unloads unreferenced assets, which would leave the objects above as destroyed
            // references. Reload everything the scene needs from disk.
            AssetDatabase.SaveAssets();
            lowPoly = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/LowPoly.mat");
            entity = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/LowPolyEntity.mat");
            glow = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/LowPolyGlow.mat");
            library = AssetDatabase.LoadAssetAtPath<PropLibrary>(LevelBaker.LibraryPath);
            if (library == null || !library.IsComplete)
                throw new System.InvalidOperationException("The prop library is missing or incomplete after baking.");

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.30f, 0.32f);
            RenderSettings.fog = false;

            // Camera: orthographic, ~60° pitch, black background.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7.2f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            var rig = camGo.AddComponent<TopDownCamera>();
            camGo.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            var composite = camGo.AddComponent<VisionComposite>();
            composite.targetCamera = cam;
            composite.compositeShader = Shader.Find("Hidden/Vision/Composite");

            // Moonlight: dim and cold. It shades surfaces but casts no shadows: the only shadows in the
            // game come from the player's flashlight.
            var moonGo = new GameObject("Moonlight");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.80f, 0.82f, 0.86f);
            moon.intensity = 1.15f;   // lower in the sky than before, so brighter to keep flat ground as lit
            moon.shadows = LightShadows.None;
            // Low enough that slopes facing away from it fall darker and the hills read.
            moonGo.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

            var visionGo = new GameObject("Vision");
            var mask = visionGo.AddComponent<VisionMaskRenderer>();
            mask.viewCamera = cam;
            mask.maskShader = Shader.Find("Hidden/Vision/Mask");
            mask.blurShader = Shader.Find("Hidden/Vision/Blur");

            var hud = camGo.AddComponent<VisionDebugHud>();
            hud.maskRenderer = mask;
            hud.composite = composite;

            var worldGo = new GameObject("Sandbox World");
            worldGo.transform.localScale = Vector3.one * WorldScale.S;
            var world = worldGo.AddComponent<SandboxWorld>();
            world.lowPolyMaterial = lowPoly;
            world.entityMaterial = entity;
            world.glowMaterial = glow;
            world.cameraRig = rig;
            world.maskRenderer = mask;
            world.library = library;

            // The 180 m level is generated when Play starts (and again by New map), not stored in the scene; this
            // also deletes any level meshes an earlier bake saved.
            world.generateOnAwake = true;
            int meshes = LevelBaker.SaveLooseMeshes(worldGo.transform);

            var gameHud = new GameObject("Game HUD").AddComponent<GameHud>();
            gameHud.world = world;
            gameHud.debugHud = hud;

            var capture = new GameObject("Vision Capture").AddComponent<VisionCapture>();
            capture.world = world;
            capture.cameraRig = rig;
            capture.composite = composite;
            capture.hud = hud;
            capture.gameHud = gameHud;
            var perf = capture.gameObject.AddComponent<VisionPerf>();
            perf.world = world;
            perf.gameHud = gameHud;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Vision] Level baked: {meshes} level meshes saved, scene at {ScenePath}");
        }

        /// <summary>
        /// No real-time shadows anywhere: no asset has a shadow of its own. The only shadows are the
        /// flashlight's (its visibility polygon and the character shadows in the vision mask).
        /// </summary>
        static void ConfigureShadows()
        {
            // Only the pipelines the game renders with, not URP assets that ship inside packages.
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var so = new SerializedObject(asset);
                so.FindProperty("m_MainLightShadowsSupported").boolValue = false;
                so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
                so.FindProperty("m_ShadowDistance").floatValue = 0f;
                so.FindProperty("m_SoftShadowsSupported").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        static Material MakeMaterial(string name, float emission, bool entity)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                mat = new Material(Shader.Find("Vision/LowPoly"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Vision/LowPoly");
            mat.SetFloat("_Emission", emission);
            mat.SetFloat("_RampSteps", 0f);   // smooth facet shading, so slopes show
            mat.SetFloat("_Entity", entity ? 1f : 0f);
            if (entity) mat.EnableKeyword("_VISION_ENTITY");
            else mat.DisableKeyword("_VISION_ENTITY");
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        [MenuItem("Vision/Build Windows Player")]
        public static void BuildWindows() => Build(BuildPath, BuildOptions.None);

        /// <summary>A development build beside the release one, for profiling (-visionPerf reads its markers).</summary>
        public static void BuildWindowsDev() => Build("Builds/WindowsDev/VisionSandbox.exe", BuildOptions.Development);

        static void Build(string path, BuildOptions buildOptions)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = buildOptions,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Vision] Build {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
