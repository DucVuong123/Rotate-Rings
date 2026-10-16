using System;
using System.Collections.Generic;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.View
{
    /// <summary>
    /// Tube meshes keyed by shape and parameters. The editor creates many variants while dragging handles,
    /// so the cache evicts the least recently used meshes past a capacity.
    /// </summary>
    public class PathMeshCache : IDisposable
    {
        const int Capacity = 96;

        readonly Dictionary<(string, ShapeParams, float), LinkedListNode<(string, ShapeParams, float, Mesh)>> _map = new();
        readonly LinkedList<(string, ShapeParams, float, Mesh)> _order = new();

        public Mesh Get(PieceModel piece) => Get(piece.Template, piece.Parameters, piece.TubeRadius, piece.Path);

        public Mesh Get(IShapeTemplate template, ShapeParams parameters, float tubeRadius, PiecePath path = null)
        {
            var key = (template.Id, parameters, tubeRadius);
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                if (node.Value.Item4 != null)
                {
                    _order.AddFirst(node);
                    return node.Value.Item4;
                }

                // Destroyed behind our back (e.g. unloaded by a scene change): build it again.
                _map.Remove(key);
            }

            var mesh = TubeMeshBuilder.Build(path ?? template.Build(parameters), tubeRadius, $"Tube_{template.Id}");
            // The cache owns the mesh's lifetime: keep Unity from unloading it as an "unused asset"
            // when a scene is opened (that left the edit-mode preview with empty meshes after Test Play).
            mesh.hideFlags = HideFlags.DontSave;
            _map[key] = _order.AddFirst((template.Id, parameters, tubeRadius, mesh));

            while (_order.Count > Capacity)
            {
                var oldest = _order.Last.Value;
                _order.RemoveLast();
                _map.Remove((oldest.Item1, oldest.Item2, oldest.Item3));
                DestroyMesh(oldest.Item4);
            }

            return mesh;
        }

        /// <summary>Also used by the edit-mode level editor, where Destroy is not allowed.</summary>
        static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(mesh);
            else
                UnityEngine.Object.DestroyImmediate(mesh);
        }

        public void Dispose()
        {
            foreach (var entry in _order)
                DestroyMesh(entry.Item4);
            _order.Clear();
            _map.Clear();
        }
    }
}
