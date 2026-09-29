using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DublinRetrofit
{
    // Makes the README screenshots reproducible. Normal runs: does nothing.
    // Run the built app with  -capture <folder>  and it selects a sample building, saves
    // "current.png" and "retrofit.png" (UI included) and quits. It goes through the same
    // ScenarioState calls as a mouse click and the toggle button, so it doubles as a smoke test.
    public class ScreenshotCapture : MonoBehaviour
    {
        [SerializeField] string preferredStreet = "Botanic Avenue";

        IEnumerator Start()
        {
            string folder = ArgumentAfter("-capture");
            if (folder == null) yield break;
            Directory.CreateDirectory(folder);
            // Launched from a script the window may never get focus, and an unfocused
            // player pauses by default; keep running so the capture can finish.
            Application.runInBackground = true;

            ScenarioState state = ScenarioState.Instance;
            while (state.Dataset == null) yield return null;
            yield return null; // CityBuilder builds in response to DatasetLoaded

            // Pick a retrofitted house on a recognisable street so both shots differ.
            BuildingView[] views = GameObject.Find("City").GetComponentsInChildren<BuildingView>();
            BuildingView sample =
                views.FirstOrDefault(v => v.Data.retrofitted && v.Data.type == "house" && v.Data.address.Contains(preferredStreet)) ??
                views.FirstOrDefault(v => v.Data.retrofitted);
            state.SetSelected(sample);
            if (sample != null)
                Camera.main.GetComponent<OrbitCamera>().FocusOn(sample.GetComponent<Renderer>().bounds.center, 260f);

            yield return Capture(Path.Combine(folder, "current.png"));
            state.SetScenario(Scenario.Retrofit);
            yield return Capture(Path.Combine(folder, "retrofit.png"));

            Debug.Log($"ScreenshotCapture: saved to {folder} (selected {sample?.Data.DisplayName})");
            Application.Quit();
        }

        static IEnumerator Capture(string path)
        {
            // Let UI layout and rendering settle, then grab the frame (including the overlay UI).
            for (int i = 0; i < 5; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
        }

        static string ArgumentAfter(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
