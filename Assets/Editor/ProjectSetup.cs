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
    // and Scenes/Main.unity with every component wired up. Re-running it rebuilds the scene,
    // so the scene never has to be edited by hand.
    // Batch mode: Unity -batchmode -projectPath . -executeMethod DublinRetrofit.EditorTools.ProjectSetup.Run -quit
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/Settings";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Dublin Retrofit/Rebuild Main Scene")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EnsureUrp();
            UseInputSystemOnly();

            Material building = CreateMaterial("Building", "Universal Render Pipeline/Lit", Color.white, 0.2f);
            Material ground = CreateGroundMaterial();
            BuildScene(building, ground);
            Debug.Log($"ProjectSetup: wrote {ScenePath}");
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
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.80f, 0.88f);
            cam.farClipPlane = 5000f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<OrbitCamera>();
            camGo.AddComponent<SelectionController>();

            var app = new GameObject("App");
            app.AddComponent<ScenarioState>();
            app.AddComponent<DatasetLoader>();
            var builder = new SerializedObject(app.AddComponent<CityBuilder>());
            builder.FindProperty("buildingMaterial").objectReferenceValue = building;
            builder.FindProperty("groundMaterial").objectReferenceValue = ground;
            builder.ApplyModifiedPropertiesWithoutUndo();
            app.AddComponent<ScreenshotCapture>();

            var hud = new GameObject("HUD");
            hud.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            hud.AddComponent<GraphicRaycaster>();
            hud.AddComponent<ScenarioToggle>();
            hud.AddComponent<InfoPanel>();
            hud.AddComponent<Legend>();
            hud.AddComponent<AboutPanel>();

            // The Input System needs its own UI input module instead of StandaloneInputModule.
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
