using UnityEngine;
using UnityEngine.UI;

namespace DublinRetrofit
{
    // Plain-language explanation for non-experts: what a BER is, what kWh/m²/yr means,
    // and which parts of the map are real, estimated or sample data.
    // Open on start so first-time viewers see it; a button in the top-left toggles it.
    public class AboutPanel : MonoBehaviour
    {
        GameObject panel;
        Text body;

        void Awake()
        {
            var topLeft = new Vector2(0f, 1f);
            UiFactory.Button(transform, "About this map", topLeft, new Vector2(16f, -16f), new Vector2(200f, 44f), Toggle);

            RectTransform rect = UiFactory.Panel(transform, "About", topLeft, new Vector2(16f, -70f), new Vector2(470f, 470f));
            body = UiFactory.Label(rect, "Text", 17, TextAnchor.UpperLeft);
            panel = rect.gameObject;
        }

        void Start()
        {
            ScenarioState state = ScenarioState.Instance;
            state.DatasetLoaded += OnDatasetLoaded;
            Refresh(state.Dataset);
        }

        void OnDestroy()
        {
            if (ScenarioState.Instance != null) ScenarioState.Instance.DatasetLoaded -= OnDatasetLoaded;
        }

        void OnDatasetLoaded(Dataset dataset) => Refresh(dataset);

        void Toggle() => panel.SetActive(!panel.activeSelf);

        void Refresh(Dataset dataset)
        {
            // The scenario numbers come from the dataset, so the text can't drift from the data.
            string share = dataset != null ? $"{dataset.meta.retrofitShare * 100f:F0} %" : "a share";
            string target = dataset != null ? dataset.meta.retrofitTarget : "B2";

            body.text =
                "<b>What am I looking at?</b>\n" +
                "Each block is a real building in Glasnevin, coloured by its <b>BER</b> (Building " +
                "Energy Rating), Ireland's energy label for buildings: from <b>A</b> (very efficient) " +
                "to <b>G</b> (least efficient). <b>kWh/m²/yr</b> is the energy a building is estimated " +
                "to use per square metre of floor each year. Lower is better.\n\n" +
                "<b>What is real?</b>\n" +
                "• <b>Real:</b> building outlines, addresses and types (OpenStreetMap).\n" +
                "• <b>Estimated:</b> most heights (storeys × 3 m).\n" +
                "• <b>SAMPLE:</b> every energy rating. Real BERs are published only in anonymised " +
                "form, so they can't be matched to individual buildings.\n" +
                "• <b>Grey:</b> sheds and garages, which don't get a BER.\n\n" +
                $"<b>After retrofit</b> shows {share} of the buildings rated below {target}, " +
                $"picked at random, upgraded to {target}.\n\n" +
                "Click a building to see its details.";
        }
    }
}
