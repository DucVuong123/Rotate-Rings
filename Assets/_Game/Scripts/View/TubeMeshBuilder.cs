using System.Collections.Generic;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    /// <summary>
    /// Sweeps a circular cross-section along a PiecePath lying in the XY plane, with hemispherical caps on
    /// open ends. Handles arcs, bars, elbows and any future path made of the same segments.
    /// </summary>
    public static class TubeMeshBuilder
    {
        const float MaxStep = 0.05f;
        const int TubeSegments = 18;
        const int CapRings = 6;

        public static Mesh Build(PiecePath path, float tubeRadius, string name = "Tube")
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            var samples = path.Sample(MaxStep);
            foreach (var sample in samples)
            {
                var side = Side(sample.Tangent);
                for (var j = 0; j <= TubeSegments; j++)
                {
                    var phi = (float)j / TubeSegments * Mathf.PI * 2f;
                    var normal = side * Mathf.Cos(phi) + Vector3.forward * Mathf.Sin(phi);
                    vertices.Add((Vector3)sample.Point + normal * tubeRadius);
                    normals.Add(normal);
                }
            }
            AddGrid(triangles, vertices, normals, 0, samples.Count - 1, TubeSegments);

            if (!path.IsClosed)
            {
                var first = samples[0];
                var last = samples[^1];
                AddCap(vertices, normals, triangles, first.Point, Side(first.Tangent), -(Vector3)first.Tangent, tubeRadius);
                AddCap(vertices, normals, triangles, last.Point, Side(last.Tangent), last.Tangent, tubeRadius);
            }

            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>In-plane direction perpendicular to the tangent.</summary>
        static Vector3 Side(Vector2 tangent) => new(tangent.y, -tangent.x, 0f);

        static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector2 center, Vector3 side, Vector3 outward, float tubeRadius)
        {
            var baseIndex = vertices.Count;
            for (var i = 0; i <= CapRings; i++)
            {
                var alpha = (float)i / CapRings * Mathf.PI * 0.5f;
                for (var j = 0; j <= TubeSegments; j++)
                {
                    var phi = (float)j / TubeSegments * Mathf.PI * 2f;
                    var around = side * Mathf.Cos(phi) + Vector3.forward * Mathf.Sin(phi);
                    var normal = (around * Mathf.Cos(alpha) + outward * Mathf.Sin(alpha)).normalized;
                    vertices.Add((Vector3)center + normal * tubeRadius);
                    normals.Add(normal);
                }
            }

            AddGrid(triangles, vertices, normals, baseIndex, CapRings, TubeSegments);
        }

        static void AddGrid(List<int> triangles, List<Vector3> vertices, List<Vector3> normals, int baseIndex, int rows, int columns)
        {
            var stride = columns + 1;
            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < columns; j++)
                {
                    var a = baseIndex + i * stride + j;
                    var b = a + stride;
                    var c = b + 1;
                    var d = a + 1;
                    AddTriangle(triangles, vertices, normals, a, b, c);
                    AddTriangle(triangles, vertices, normals, a, c, d);
                }
            }
        }

        /// <summary>Adds a triangle wound clockwise as seen from outside (Unity's front face).</summary>
        static void AddTriangle(List<int> triangles, List<Vector3> vertices, List<Vector3> normals, int a, int b, int c)
        {
            var faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (faceNormal.sqrMagnitude < 1e-12f)
                return;

            var outward = normals[a] + normals[b] + normals[c];
            if (Vector3.Dot(faceNormal, outward) > 0f)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }
    }
}
