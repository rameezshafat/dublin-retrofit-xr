using System.Collections.Generic;
using UnityEngine;

namespace DublinRetrofit
{
    // Ear-clipping triangulation for a simple polygon (no holes).
    // An "ear" is a convex corner whose triangle contains no other polygon vertex;
    // cutting it off leaves a smaller simple polygon, so we repeat until 3 vertices remain.
    // O(n^3) worst case, which is fine for building footprints (typically 4-20 vertices).
    public static class PolygonTriangulator
    {
        const float Epsilon = 1e-6f;

        // points: polygon in counter-clockwise order (x = east, y = north).
        // Returns index triples, each triangle also counter-clockwise.
        public static List<int> Triangulate(IReadOnlyList<Vector2> points)
        {
            var triangles = new List<int>();
            var remaining = new List<int>();
            for (int i = 0; i < points.Count; i++) remaining.Add(i);

            // Guard against infinite loops on malformed input.
            int guard = points.Count * points.Count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                int n = remaining.Count;
                for (int i = 0; i < n; i++)
                {
                    int prev = remaining[(i - 1 + n) % n];
                    int cur = remaining[i];
                    int next = remaining[(i + 1) % n];
                    float cross = Cross(points[prev], points[cur], points[next]);

                    // A collinear vertex adds nothing to the roof: drop it without a triangle.
                    if (Mathf.Abs(cross) < Epsilon) { remaining.RemoveAt(i); clipped = true; break; }

                    // Negative cross = reflex (inward) corner in a CCW polygon: not an ear.
                    if (cross < 0f) continue;
                    if (ContainsOtherVertex(points, remaining, prev, cur, next)) continue;

                    triangles.Add(prev); triangles.Add(cur); triangles.Add(next);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                // Self-touching OSM outlines can leave no valid ear. Fan the rest so the
                // building still gets a (possibly imperfect) roof instead of a hole.
                if (!clipped)
                {
                    for (int i = 1; i < remaining.Count - 1; i++)
                    {
                        triangles.Add(remaining[0]); triangles.Add(remaining[i]); triangles.Add(remaining[i + 1]);
                    }
                    return triangles;
                }
            }

            if (remaining.Count == 3)
            {
                triangles.Add(remaining[0]); triangles.Add(remaining[1]); triangles.Add(remaining[2]);
            }
            return triangles;
        }

        // z-component of (b - a) x (c - b): positive when a->b->c turns left (counter-clockwise).
        static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);

        static bool ContainsOtherVertex(IReadOnlyList<Vector2> pts, List<int> remaining, int a, int b, int c)
        {
            foreach (int i in remaining)
            {
                if (i == a || i == b || i == c) continue;
                if (InTriangle(pts[i], pts[a], pts[b], pts[c])) return true;
            }
            return false;
        }

        // Point is inside (or on the edge of) a CCW triangle if it is left of all three edges.
        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Side(a, b, p) >= 0f && Side(b, c, p) >= 0f && Side(c, a, p) >= 0f;

        static float Side(Vector2 a, Vector2 b, Vector2 p) =>
            (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }
}
