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

        [MenuItem("Vision/Create Sandbox Scene")]
        public static void CreateSandbox()
        {
            ConfigureShadows();
            Material voxel = MakeMaterial("Voxel", 0f, false);
            Material entity = MakeMaterial("VoxelEntity", 0f, true);
            Material glow = MakeMaterial("VoxelGlow", 0.6f, false);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.30f, 0.32f);
            RenderSettings.fog = false;

            // Camera: orthographic, ~80° pitch, black background.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            var rig = camGo.AddComponent<TopDownCamera>();
            camGo.transform.rotation = Quaternion.Euler(80f, 0f, 0f);

            var composite = camGo.AddComponent<VisionComposite>();
            composite.targetCamera = cam;
            composite.compositeShader = Shader.Find("Hidden/Vision/Composite");

            // Moonlight: dim, cold, casts the branch shadows.
            var moonGo = new GameObject("Moonlight");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.80f, 0.82f, 0.86f);
            moon.intensity = 0.85f;
            moon.shadows = LightShadows.Soft;
            moonGo.transform.rotation = Quaternion.Euler(58f, -35f, 0f);

            var visionGo = new GameObject("Vision");
            var mask = visionGo.AddComponent<VisionMaskRenderer>();
            mask.viewCamera = cam;
            mask.maskShader = Shader.Find("Hidden/Vision/Mask");
            mask.blurShader = Shader.Find("Hidden/Vision/Blur");

            var hud = camGo.AddComponent<VisionDebugHud>();
            hud.maskRenderer = mask;
            hud.composite = composite;

            var worldGo = new GameObject("Sandbox World");
            var world = worldGo.AddComponent<SandboxWorld>();
            world.voxelMaterial = voxel;
            world.entityMaterial = entity;
            world.glowMaterial = glow;
            world.cameraRig = rig;
            world.maskRenderer = mask;

            var capture = new GameObject("Vision Capture").AddComponent<VisionCapture>();
            capture.world = world;
            capture.cameraRig = rig;
            capture.composite = composite;
            capture.hud = hud;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[Vision] Sandbox scene created at " + ScenePath);
        }

        /// <summary>
        /// The ortho camera sits ~20 m above the ground, so one cascade over a short distance puts all the
        /// shadow-map resolution on the diorama instead of on empty air near the camera.
        /// </summary>
        static void ConfigureShadows()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var so = new SerializedObject(asset);
                so.FindProperty("m_ShadowDistance").floatValue = 42f;
                so.FindProperty("m_ShadowCascadeCount").intValue = 1;
                so.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
                so.FindProperty("m_SoftShadowsSupported").boolValue = true;
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
                mat = new Material(Shader.Find("Vision/Voxel"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("Vision/Voxel");
            mat.SetFloat("_Emission", emission);
            mat.SetFloat("_Entity", entity ? 1f : 0f);
            if (entity) mat.EnableKeyword("_VISION_ENTITY");
            else mat.DisableKeyword("_VISION_ENTITY");
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        [MenuItem("Vision/Build Windows Player")]
        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Vision] Build {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
