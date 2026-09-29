using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DublinRetrofit.EditorTools
{
    // One-click (or command-line) project setup: URP pipeline asset, the two materials,
    // and the desktop (Main) and XR (MainXR) scenes with every component wired up. Re-running it
    // rebuilds them, so the scenes never have to be edited by hand.
    // Batch mode: Unity -batchmode -projectPath . -executeMethod DublinRetrofit.EditorTools.ProjectSetup.Run -quit
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/Settings";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Dublin Retrofit/Rebuild Scenes")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EnsureUrp();
            UseInputSystemOnly();

            Material building = CreateMaterial("Building", "Universal Render Pipeline/Lit", Color.white, 0.2f);
            Material ground = CreateGroundMaterial();
            BuildScene(building, ground);
            XrSceneSetup.BuildScene(building, ground);

            // The desktop scene loads first; the XR scene is included for builds that want it.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(XrSceneSetup.ScenePath, true),
            };
            Debug.Log($"ProjectSetup: wrote {ScenePath} and {XrSceneSetup.ScenePath}");
        }

        // Use the desktop URP asset from Unity's URP template for every quality level,
        // so the app looks the same whichever quality level is active.
        static void EnsureUrp()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"{SettingsDir}/PC_RPAsset.asset");
            if (pipeline == null)
            {
                Debug.LogError("ProjectSetup: Assets/Settings/PC_RPAsset.asset is missing.");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            AssetDatabase.SaveAssets();
        }

        // Unity 6 uses the Input System package; turn the old Input Manager off so the
        // project matches the brief (0 = old, 1 = new, 2 = both). Takes effect on next launch.
        static void UseInputSystemOnly()
        {
            Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            var so = new SerializedObject(settings);
            SerializedProperty handler = so.FindProperty("activeInputHandler");
            if (handler == null) return;
            handler.intValue = 1;
            so.ApplyModifiedProperties();
        }

        // The street map is shown unlit: it is a picture of the ground, so it should look
        // exactly like the map rather than be shaded by the sun.
        static Material CreateGroundMaterial()
        {
            const string texturePath = "Assets/Textures/basemap.png";
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
            {
                importer.maxTextureSize = 4096;                      // keep full resolution (~2300 px)
                importer.npotScale = TextureImporterNPOTScale.None;  // don't squash to a power of two
                importer.anisoLevel = 8;                             // sharp when viewed at an angle
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            Material material = CreateMaterial("Ground", "Universal Render Pipeline/Unlit", Color.white, 0f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            return material;
        }

        static Material CreateMaterial(string name, string shaderName, Color color, float smoothness)
        {
            string path = $"{SettingsDir}/{name}.mat";
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BuildScene(Material building, Material ground)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLighting();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.farClipPlane = 5000f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<OrbitCamera>();
            camGo.AddComponent<SelectionController>();

            CreateApp(building, ground, 1f, Vector3.zero);

            var hud = new GameObject("HUD");
            hud.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            hud.AddComponent<GraphicRaycaster>();
            AddPanels(hud);

            // The Input System needs its own UI input module instead of StandaloneInputModule.
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---- Shared by the desktop scene and the XR scene (XrSceneSetup) ----

        internal static readonly Color SkyColor = new Color(0.72f, 0.80f, 0.88f);

        internal static void AddLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Data + state + city: identical in both scenes apart from the model's scale/position.
        internal static GameObject CreateApp(Material building, Material ground, float scale, Vector3 position)
        {
            var app = new GameObject("App");
            app.AddComponent<ScenarioState>();
            app.AddComponent<DatasetLoader>();
            var builder = new SerializedObject(app.AddComponent<CityBuilder>());
            builder.FindProperty("buildingMaterial").objectReferenceValue = building;
            builder.FindProperty("groundMaterial").objectReferenceValue = ground;
            builder.FindProperty("modelScale").floatValue = scale;
            builder.FindProperty("modelPosition").vector3Value = position;
            builder.ApplyModifiedPropertiesWithoutUndo();
            app.AddComponent<ScreenshotCapture>();
            return app;
        }

        internal static void AddPanels(GameObject canvas)
        {
            canvas.AddComponent<ScenarioToggle>();
            canvas.AddComponent<InfoPanel>();
            canvas.AddComponent<Legend>();
            canvas.AddComponent<AboutPanel>();
        }
    }
}
