using UnityEngine;
using UnityEngine.UI;

namespace DublinRetrofit
{
    // Two buttons at the top of the screen: "Current" and "After retrofit".
    // Clicking one writes the scenario to ScenarioState; the buttons then restyle themselves
    // from the ScenarioChanged event, so the UI can never disagree with the actual state.
    public class ScenarioToggle : MonoBehaviour
    {
        static readonly Color ActiveColor = new Color(0.16f, 0.47f, 0.85f, 0.95f);

        Button currentButton;
        Button retrofitButton;

        void Awake()
        {
            var size = new Vector2(190f, 44f);
            var anchor = new Vector2(0.5f, 1f); // top-centre
            currentButton = UiFactory.Button(transform, "Current", anchor, new Vector2(-98f, -16f), size,
                () => ScenarioState.Instance.SetScenario(Scenario.Current));
            retrofitButton = UiFactory.Button(transform, "After retrofit", anchor, new Vector2(98f, -16f), size,
                () => ScenarioState.Instance.SetScenario(Scenario.Retrofit));
        }

        void Start()
        {
            ScenarioState.Instance.ScenarioChanged += Restyle;
            Restyle(ScenarioState.Instance.Scenario);
        }

        void OnDestroy()
        {
            if (ScenarioState.Instance != null) ScenarioState.Instance.ScenarioChanged -= Restyle;
        }

        void Restyle(Scenario scenario)
        {
            Paint(currentButton, scenario == Scenario.Current);
            Paint(retrofitButton, scenario == Scenario.Retrofit);
        }

        static void Paint(Button button, bool active)
        {
            button.GetComponent<Image>().color = active ? ActiveColor : UiFactory.PanelColor;
        }
    }
}
