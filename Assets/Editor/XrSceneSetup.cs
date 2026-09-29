using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace DublinRetrofit.EditorTools
{
    // Builds Scenes/MainXR.unity: the same app as the desktop scene, shown as a 1:500 tabletop
    // model (the usual way to present a neighbourhood-scale digital twin in VR) with the
    // panels on a world-space board behind the table. Uses the XR Interaction Toolkit's
    // Starter Assets rig and its XR Interaction Simulator, so it can be tried without a headset.
    public static class XrSceneSetup
    {
        internal const string ScenePath = "Assets/Scenes/MainXR.unity";
        const string Samples = "Assets/Samples/XR Interaction Toolkit/3.6.0";

        const float ModelScale = 0.002f;                          // 1:500 -> the area is ~1.6 x 1.3 m
        static readonly Vector3 TableTop = new Vector3(0f, 0.8f, 1.1f);
        static readonly Vector3 BoardPosition = new Vector3(0f, 1.5f, 2.2f);
        const float BoardScale = 0.0011f;
        const float EyeHeight = 1.4f;                         // 1920 x 1080 UI units -> 2.1 x 1.2 m

        internal static void BuildScene(Material building, Material ground)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ProjectSetup.AddLighting();

            // XR rig (headset camera + two controllers with ray interactors) and the simulator
            // that drives it from keyboard and mouse when no headset is connected.
            GameObject rig = Instantiate($"{Samples}/Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
            Instantiate($"{Samples}/XR Interaction Simulator/XR Interaction Simulator.prefab");
            // Device origin + fixed eye height: the table sits at a comfortable height whether
            // the viewer is seated, standing, or using the simulator (which has no floor).
            var origin = rig.GetComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = EyeHeight;
            Camera head = rig.GetComponentInChildren<Camera>();
            head.clearFlags = CameraClearFlags.SolidColor;
            head.backgroundColor = ProjectSetup.SkyColor;

            ProjectSetup.CreateApp(building, ground, ModelScale, TableTop)
                .AddComponent<XrBuildingInteraction>();
            CreateTable();
            CreateBoard(head);

            // XRUIInputModule lets controller rays (and the mouse) press the UI buttons.
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<XRUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static GameObject Instantiate(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.IO.FileNotFoundException($"XR sample prefab missing: {path}");
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        // A plain box under the map so the model reads as a physical tabletop.
        static void CreateTable()
        {
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.position = new Vector3(TableTop.x, TableTop.y / 2f - 0.005f, TableTop.z);
            table.transform.localScale = new Vector3(1.8f, TableTop.y - 0.01f, 1.45f);
        }

        // The same panels as the desktop HUD, on a world-space canvas. Text in world space
        // needs extra pixels per unit to stay sharp; the tracked-device raycaster lets
        // controller rays hit the buttons.
        static void CreateBoard(Camera head)
        {
            var board = new GameObject("Board", typeof(RectTransform));
            var canvas = board.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = head;

            board.AddComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 3f;
            board.AddComponent<TrackedDeviceGraphicRaycaster>();
            ProjectSetup.AddPanels(board);

            // A root RectTransform is placed through anchoredPosition3D; setting .position alone
            // is overwritten by the layout (the board ended up on the floor).
            var rect = (RectTransform)board.transform;
            rect.sizeDelta = new Vector2(1920f, 1080f);
            rect.localScale = Vector3.one * BoardScale;
            rect.anchoredPosition3D = BoardPosition;
        }
    }
}
