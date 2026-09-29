using UnityEngine;
using UnityEngine.UI;

namespace DublinRetrofit
{
    // Bottom-left legend: one colour swatch per BER letter (plus grey for "not rated") and
    // how many buildings fall in each for the scenario on screen.
    // Also owns the permanent sample-data disclaimer.
    public class Legend : MonoBehaviour
    {
        public const string Disclaimer =
            "Energy ratings are SAMPLE data, not real BER ratings. Map data & footprints © OpenStreetMap contributors.";

        const float RowHeight = 26f;

        Text title;
        // One label per BER letter, plus a final row for unrated buildings.
        readonly Text[] rowLabels = new Text[BerPalette.Letters.Length + 1];

        void Awake()
        {
            int rows = rowLabels.Length;
            var size = new Vector2(270f, 56f + rows * RowHeight);
            RectTransform panel = UiFactory.Panel(transform, "Legend", Vector2.zero, new Vector2(16f, 56f), size);

            title = UiFactory.Label(panel, "Title", 17, TextAnchor.UpperLeft);
            for (int i = 0; i < rows; i++)
            {
                float y = -46f - i * RowHeight;
                bool notRatedRow = i == BerPalette.Letters.Length;
                Color color = notRatedRow ? BerPalette.NotRated : BerPalette.ColorForLetter(i);
                UiFactory.Swatch(panel, new Vector2(UiFactory.Padding, y), 18f, color);

                // Each row label gets its own strip to the right of the swatch.
                Text label = UiFactory.Label(panel, $"Row {i}", 16, TextAnchor.UpperLeft, 0f);
                RectTransform rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(UiFactory.Padding + 28f, y + 1f);
                rect.sizeDelta = new Vector2(size.x - 60f, RowHeight);
                rowLabels[i] = label;
            }

            // Full-width strip along the bottom edge that is never hidden.
            RectTransform strip = UiFactory.Panel(transform, "Disclaimer", Vector2.zero, Vector2.zero, Vector2.zero);
            strip.anchorMax = new Vector2(1f, 0f);
            strip.sizeDelta = new Vector2(0f, 40f);
            UiFactory.Label(strip, "Text", 16, TextAnchor.MiddleCenter, 6f).text = Disclaimer;
        }

        void Start()
        {
            ScenarioState state = ScenarioState.Instance;
            state.DatasetLoaded += OnDatasetLoaded;
            state.ScenarioChanged += OnScenarioChanged;
            Refresh();
        }

        void OnDestroy()
        {
            ScenarioState state = ScenarioState.Instance;
            if (state == null) return;
            state.DatasetLoaded -= OnDatasetLoaded;
            state.ScenarioChanged -= OnScenarioChanged;
        }

        void OnDatasetLoaded(Dataset _) => Refresh();
        void OnScenarioChanged(Scenario _) => Refresh();

        void Refresh()
        {
            ScenarioState state = ScenarioState.Instance;
            string scenarioName = state.Scenario == Scenario.Retrofit ? "after retrofit" : "current";
            title.text = $"<b>BER band</b> ({scenarioName})";

            // Count buildings per letter for the scenario currently shown.
            // Unrated buildings have an empty label, so LetterIndex returns -1 for them.
            var counts = new int[rowLabels.Length];
            if (state.Dataset != null)
            {
                foreach (BuildingRecord b in state.Dataset.buildings)
                {
                    int letter = BerPalette.LetterIndex(b.Ber(state.Scenario));
                    counts[letter >= 0 ? letter : rowLabels.Length - 1]++;
                }
            }

            for (int i = 0; i < BerPalette.Letters.Length; i++)
                rowLabels[i].text = $"{BerPalette.Letters[i]}    {counts[i]} buildings";
            rowLabels[rowLabels.Length - 1].text = $"Not rated    {counts[rowLabels.Length - 1]} outbuildings";
        }
    }
}
