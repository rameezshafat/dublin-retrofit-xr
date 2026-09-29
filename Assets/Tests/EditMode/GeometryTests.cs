using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DublinRetrofit.Tests
{
    // Checks the two things that silently break building geometry:
    // triangulation (holes/overlaps in roofs) and winding (faces pointing the wrong way).
    public class GeometryTests
    {
        static readonly Vector2[] Square = { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };

        // L-shape: concave, so a naive fan triangulation would cover the missing corner.
        static readonly Vector2[] LShape = { new(0, 0), new(20, 0), new(20, 10), new(10, 10), new(10, 20), new(0, 20) };

        [Test]
        public void Square_TriangulatesToTwoTrianglesCoveringItsArea()
        {
            List<int> tris = PolygonTriangulator.Triangulate(Square);
            Assert.AreEqual(6, tris.Count);
            Assert.AreEqual(100f, TriangleArea(Square, tris), 0.01f);
        }

        [Test]
        public void ConcaveLShape_KeepsExactArea()
        {
            List<int> tris = PolygonTriangulator.Triangulate(LShape);
            Assert.AreEqual(4 * 3, tris.Count);                       // n - 2 triangles
            Assert.AreEqual(300f, TriangleArea(LShape, tris), 0.01f); // not 400: corner stays empty
        }

        [Test]
        public void CollinearPoint_IsDroppedWithoutBreakingTheRoof()
        {
            var withMidpoint = new[] { new Vector2(0, 0), new Vector2(5, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) };
            Assert.AreEqual(100f, TriangleArea(withMidpoint, PolygonTriangulator.Triangulate(withMidpoint)), 0.01f);
        }

        [Test]
        public void EveryTriangle_FacesOutwards()
        {
            var footprint = new List<FootprintPoint>();
            foreach (Vector2 p in LShape) footprint.Add(new FootprintPoint { x = p.x, z = p.y });
            Mesh mesh = BuildingMeshFactory.Build(footprint.ToArray(), 6f);

            // Unity draws the side where Cross(b - a, c - a) points, so each face normal must
            // agree with the stored vertex normal (up for the roof, outward for the walls).
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Assert.Greater(Vector3.Dot(face, n[t[i]]), 0f, $"triangle {i / 3} faces inwards");
            }
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void BerPalette_DOrWorseThreshold()
        {
            Assert.IsTrue(BerPalette.IsDOrWorse("D1"));
            Assert.IsTrue(BerPalette.IsDOrWorse("G"));
            Assert.IsFalse(BerPalette.IsDOrWorse("C3"));
            Assert.IsFalse(BerPalette.IsDOrWorse(""));   // unrated buildings are never "poor"
            Assert.AreEqual(BerPalette.NotRated, BerPalette.ColorFor(""));
        }

        // Signed area of the triangles, using the same x/y (east/north) coordinates as the input.
        static float TriangleArea(IReadOnlyList<Vector2> p, List<int> tris)
        {
            float area = 0f;
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector2 a = p[tris[i]], b = p[tris[i + 1]], c = p[tris[i + 2]];
                area += 0.5f * ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            }
            return area;
        }
    }
}
