using System;
using UnityEngine;

namespace DublinRetrofit
{
    public enum Scenario { Current, Retrofit }

    // Single source of truth for app state: the loaded dataset, which scenario is shown,
    // and which building is hovered/selected. Other scripts never talk to each other
    // directly; they write here and listen to the events. This is the only singleton.
    public class ScenarioState : MonoBehaviour
    {
        public static ScenarioState Instance { get; private set; }

        public Dataset Dataset { get; private set; }
        public Scenario Scenario { get; private set; } = Scenario.Current;
        public BuildingView Selected { get; private set; }
        public BuildingView Hovered { get; private set; }

        public event Action<Dataset> DatasetLoaded;
        public event Action<Scenario> ScenarioChanged;
        public event Action<BuildingView> SelectionChanged;
        public event Action<BuildingView> HoverChanged;

        void Awake()
        {
            // Awake runs before any Start, so listeners can safely subscribe in Start.
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetDataset(Dataset dataset)
        {
            Dataset = dataset;
            DatasetLoaded?.Invoke(dataset);
        }

        // Each setter ignores no-op changes so listeners only redraw when something changed.
        public void SetScenario(Scenario scenario)
        {
            if (scenario == Scenario) return;
            Scenario = scenario;
            ScenarioChanged?.Invoke(scenario);
        }

        public void SetSelected(BuildingView view)
        {
            if (view == Selected) return;
            Selected = view;
            SelectionChanged?.Invoke(view);
        }

        public void SetHovered(BuildingView view)
        {
            if (view == Hovered) return;
            Hovered = view;
            HoverChanged?.Invoke(view);
        }
    }
}
