using UnityEngine;
using UnityEngine.UI;

namespace DublinRetrofit
{
    // Right-hand side panel with two parts:
    //  - the selected building's data (both scenarios, the one on screen in bold)
    //  - an area summary comparing the whole neighbourhood now vs after retrofit.
    // It only reads from ScenarioState and redraws on its events.
    public class InfoPanel : MonoBehaviour
    {
        Text buildingText;
        Text summaryText;

        void Awake()
        {
            var topRight = Vector2.one;
            RectTransform building = UiFactory.Panel(transform, "Building", topRight, new Vector2(-16f, -16f), new Vector2(420f, 285f));
            buildingText = UiFactory.Label(building, "Text", 18, TextAnchor.UpperLeft);

            RectTransform summary = UiFactory.Panel(transform, "Summary", topRight, new Vector2(-16f, -313f), new Vector2(420f, 262f));
            summaryText = UiFactory.Label(summary, "Text", 18, TextAnchor.UpperLeft);
        }

        void Start()
        {
            ScenarioState state = ScenarioState.Instance;
            state.DatasetLoaded += OnDatasetLoaded;
            state.ScenarioChanged += OnScenarioChanged;
            state.SelectionChanged += OnSelectionChanged;
            Refresh();
        }

        void OnDestroy()
        {
            ScenarioState state = ScenarioState.Instance;
            if (state == null) return;
            state.DatasetLoaded -= OnDatasetLoaded;
            state.ScenarioChanged -= OnScenarioChanged;
            state.SelectionChanged -= OnSelectionChanged;
        }

        void OnDatasetLoaded(Dataset _) => Refresh();
        void OnScenarioChanged(Scenario _) => Refresh();
        void OnSelectionChanged(BuildingView _) => Refresh();

        void Refresh()
        {
            ScenarioState state = ScenarioState.Instance;
            buildingText.text = state.Selected != null
                ? DescribeBuilding(state.Selected.Data, state.Scenario)
                : "<b>No building selected</b>\n\nClick a building to see its data.\n\n" +
                  "Right-drag: orbit\nMiddle-drag: pan\nScroll: zoom\nF: focus on selected building";
            summaryText.text = state.Dataset != null ? DescribeArea(state.Dataset) : "Loading data...";
        }

        static string DescribeBuilding(BuildingRecord b, Scenario shown)
        {
            string header = $"<b>{b.DisplayName}</b>\n" +
                            $"Type: {b.DisplayType}\n" +
                            $"Footprint area: {b.footprintArea:F0} m²\n" +
                            $"Height: {b.height:F1} m ({b.HeightNote})\n\n";

            if (!b.rated)
                return header + "<b>Not rated.</b> Outbuildings such as sheds and garages don't get a BER, " +
                                "so this one is grey and left out of the area summary.";

            string now = ScenarioLine("Current", b.ber, b.kwhPerM2Yr, shown == Scenario.Current);
            string after = b.retrofitted
                ? ScenarioLine("After retrofit", b.retrofitBer, b.retrofitKwhPerM2Yr, shown == Scenario.Retrofit)
                : "   After retrofit: not upgraded in this scenario";
            string reduction = b.retrofitted ? $"Reduction: <b>{b.ReductionPercent:F0} %</b>\n" : "";
            return header + $"{now}\n{after}\n\n{reduction}" +
                   $"<size=13>OSM id {b.osmId} · energy values are SAMPLE data</size>";
        }

        // e.g. "■ Current: D1 · 243 kWh/m²/yr", bold if this is the scenario on screen.
        static string ScenarioLine(string name, string ber, float kwh, bool bold)
        {
            string swatch = $"<color={UiFactory.Hex(BerPalette.ColorFor(ber))}>■</color>";
            string line = $"{name}: BER {ber} · {kwh:F0} kWh/m²/yr";
            return bold ? $"{swatch} <b>{line}</b>" : $"{swatch} {line}";
        }

        static string DescribeArea(Dataset dataset)
        {
            int rated = 0, unrated = 0, retrofitted = 0, poorNow = 0, poorAfter = 0;
            float floor = 0f, weightedNow = 0f, weightedAfter = 0f;
            foreach (BuildingRecord b in dataset.buildings)
            {
                if (!b.rated) { unrated++; continue; } // no BER, so no energy figures
                rated++;
                if (b.retrofitted) retrofitted++;

                // kWh/m²/yr is per square metre of floor, so a fair area-wide figure
                // weights each building by its floor area (big buildings count more).
                floor += b.floorArea;
                weightedNow += b.kwhPerM2Yr * b.floorArea;
                weightedAfter += b.retrofitKwhPerM2Yr * b.floorArea;
                if (BerPalette.IsDOrWorse(b.ber)) poorNow++;
                if (BerPalette.IsDOrWorse(b.retrofitBer)) poorAfter++;
            }

            float avgNow = floor > 0f ? weightedNow / floor : 0f;
            float avgAfter = floor > 0f ? weightedAfter / floor : 0f;
            float change = avgNow > 0f ? 100f * (avgNow - avgAfter) / avgNow : 0f;
            DatasetMeta meta = dataset.meta;

            return $"<b>Area summary: {meta.area}</b>\n" +
                   $"Rated buildings: {rated}  ({unrated} outbuildings not rated)\n" +
                   $"Upgraded in scenario: {retrofitted} ({meta.retrofitShare * 100f:F0} % of those below {meta.retrofitTarget})\n\n" +
                   $"Energy use, weighted by floor area:\n  now {avgNow:F0}  →  after {avgAfter:F0} kWh/m²/yr  (−{change:F0} %)\n" +
                   $"Rated D or worse:\n  now {Percent(poorNow, rated)}  →  after {Percent(poorAfter, rated)}";
        }

        static string Percent(int part, int whole) => whole > 0 ? $"{100f * part / whole:F0} %" : "–";
    }
}
