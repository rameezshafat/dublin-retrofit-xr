using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DublinRetrofit.Tests
{
    // Checks the real buildings.json: it parses, every roof triangulates to its footprint
    // area, and the energy fields obey the rules the Python script is meant to apply.
    public class DatasetTests
    {
        Dataset dataset;

        [OneTimeSetUp]
        public void Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "buildings.json");
            dataset = JsonUtility.FromJson<Dataset>(File.ReadAllText(path));
        }

        [Test]
        public void Parses_WithDeclaredBuildingCount()
        {
            Assert.IsNotNull(dataset.buildings);
            Assert.AreEqual(dataset.meta.buildingCount, dataset.buildings.Length);
        }

        [Test]
        public void EveryRoof_MatchesItsFootprintArea()
        {
            foreach (BuildingRecord b in dataset.buildings)
            {
                var pts = new List<Vector2>();
                foreach (FootprintPoint p in b.footprint) pts.Add(new Vector2(p.x, p.z));
                List<int> tris = PolygonTriangulator.Triangulate(pts);

                float area = 0f;
                for (int t = 0; t < tris.Count; t += 3)
                {
                    Vector2 a = pts[tris[t]], c = pts[tris[t + 1]], d = pts[tris[t + 2]];
                    area += 0.5f * ((c.x - a.x) * (d.y - a.y) - (c.y - a.y) * (d.x - a.x));
                }
                // 1 % tolerance: coordinates are rounded to 1 cm in the JSON.
                Assert.AreEqual(b.footprintArea, area, 0.01f * b.footprintArea + 0.5f, $"building {b.id}");
            }
        }

        [Test]
        public void EnergyFields_FollowTheScenarioRules()
        {
            foreach (BuildingRecord b in dataset.buildings)
            {
                if (!b.rated)
                {
                    Assert.IsEmpty(b.ber, $"unrated building {b.id} has a BER");
                    Assert.IsFalse(b.retrofitted);
                    continue;
                }
                Assert.GreaterOrEqual(BerPalette.Rank(b.ber), 0, $"building {b.id}: unknown BER '{b.ber}'");
                // Retrofit never makes a building worse; untouched buildings stay identical.
                Assert.LessOrEqual(b.retrofitKwhPerM2Yr, b.kwhPerM2Yr, $"building {b.id}");
                if (!b.retrofitted) Assert.AreEqual(b.ber, b.retrofitBer, $"building {b.id}");
            }
        }
    }
}
