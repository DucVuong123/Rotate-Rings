using System;
using System.Collections.Generic;

namespace RotateRings.Gameplay.Shapes
{
    public class ShapeRegistry
    {
        readonly Dictionary<string, IShapeTemplate> _templates = new();
        readonly List<IShapeTemplate> _ordered = new();

        public IReadOnlyList<IShapeTemplate> All => _ordered;

        public ShapeRegistry(IEnumerable<IShapeTemplate> templates)
        {
            foreach (var template in templates)
            {
                _templates.Add(template.Id, template);
                _ordered.Add(template);
            }
        }

        /// <summary>Every built-in shape. New shapes are added here.</summary>
        public static ShapeRegistry CreateDefault() =>
            new(new IShapeTemplate[] { new ArcShape(), new BarShape(), new ElbowShape() });

        public IShapeTemplate Get(string id) =>
            _templates.TryGetValue(id ?? string.Empty, out var template)
                ? template
                : throw new ArgumentException($"Unknown shape '{id}'.");

        public bool TryGet(string id, out IShapeTemplate template) => _templates.TryGetValue(id ?? string.Empty, out template);
    }
}
