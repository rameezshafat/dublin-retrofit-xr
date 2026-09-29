using System.Collections.Generic;
using UnityEngine;

namespace DublinRetrofit
{
    // Turns a 2D footprint + height into a closed prism mesh (flat roof + vertical walls).
    // Vertices are in world metres (x = east, z = north, y = up), so every building
    // GameObject can sit at the origin and still appear in the right place.
    public static class BuildingMeshFactory
    {
        public static Mesh Build(FootprintPoint[] footprint, float height)
        {
            int n = footprint.Length;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // --- Roof: one vertex per footprint point, all facing straight up.
            var flat = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                flat[i] = new Vector2(footprint[i].x, footprint[i].z);
                vertices.Add(new Vector3(footprint[i].x, height, footprint[i].z));
                normals.Add(Vector3.up);
            }

            // The triangulator returns counter-clockwise triangles (seen from above).
            // Unity treats clockwise-as-seen-by-the-camera as the front face, so we swap
            // the last two indices to make the roof visible from above.
            List<int> roof = PolygonTriangulator.Triangulate(flat);
            for (int t = 0; t < roof.Count; t += 3)
            {
                triangles.Add(roof[t]); triangles.Add(roof[t + 2]); triangles.Add(roof[t + 1]);
            }

            // --- Walls: one quad per edge. Each quad gets its own 4 vertices so it can have
            // a flat outward normal (shared vertices would smooth the corners).
            for (int i = 0; i < n; i++)
            {
                Vector2 a = flat[i];
                Vector2 b = flat[(i + 1) % n];
                Vector2 edge = b - a;
                if (edge.sqrMagnitude < 1e-6f) continue; // skip duplicate points

                // For a CCW polygon, the outside is to the right of each edge: (dz, -dx).
                Vector3 outward = new Vector3(edge.y, 0f, -edge.x).normalized;

                int start = vertices.Count;
                vertices.Add(new Vector3(a.x, 0f, a.y));     // 0 bottom-left (seen from outside)
                vertices.Add(new Vector3(b.x, 0f, b.y));     // 1 bottom-right
                vertices.Add(new Vector3(b.x, height, b.y)); // 2 top-right
                vertices.Add(new Vector3(a.x, height, a.y)); // 3 top-left
                for (int k = 0; k < 4; k++) normals.Add(outward);

                // Clockwise when viewed from outside: top-left, top-right, bottom-right, ...
                triangles.Add(start + 3); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 3); triangles.Add(start + 1); triangles.Add(start + 0);
            }

            var mesh = new Mesh { name = "Building" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
