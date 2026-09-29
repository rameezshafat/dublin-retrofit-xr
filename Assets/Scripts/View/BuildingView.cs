using UnityEngine;

namespace DublinRetrofit
{
    // Lives on every building GameObject. Holds that building's data record and
    // repaints itself whenever the scenario, selection or hover changes.
    [RequireComponent(typeof(MeshRenderer))]
    public class BuildingView : MonoBehaviour
    {
        // URP Lit uses _BaseColor; the built-in Standard shader uses _Color. Setting both
        // keeps the view working if the project falls back to the built-in pipeline.
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        const float HoverTint = 0.35f;    // how far towards white a hovered building goes
        const float SelectedTint = 0.7f;  // selected buildings are clearly paler

        public BuildingRecord Data { get; private set; }

        ScenarioState state;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;

        public void Init(BuildingRecord data, ScenarioState scenarioState)
        {
            Data = data;
            state = scenarioState;
            meshRenderer = GetComponent<MeshRenderer>();
            // A property block overrides the colour per renderer without creating a new
            // material, so all buildings keep sharing one material (and can be batched).
            block = new MaterialPropertyBlock();

            state.ScenarioChanged += OnScenarioChanged;
            state.SelectionChanged += OnViewChanged;
            state.HoverChanged += OnViewChanged;
            Refresh();
        }

        void OnDestroy()
        {
            if (state == null) return;
            state.ScenarioChanged -= OnScenarioChanged;
            state.SelectionChanged -= OnViewChanged;
            state.HoverChanged -= OnViewChanged;
        }

        void OnScenarioChanged(Scenario _) => Refresh();
        void OnViewChanged(BuildingView _) => Refresh();

        void Refresh()
        {
            Color color = BerPalette.ColorFor(Data.Ber(state.Scenario));
            if (state.Selected == this) color = Color.Lerp(color, Color.white, SelectedTint);
            else if (state.Hovered == this) color = Color.Lerp(color, Color.white, HoverTint);

            meshRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            meshRenderer.SetPropertyBlock(block);
        }
    }
}
