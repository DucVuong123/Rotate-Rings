using System;
using System.Collections.Generic;
using GameCore.Domain.Factory;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Shapes;

namespace RotateRings.Gameplay.Model
{
    public class BoardModelFactory : IFactory<LevelDefinition, BoardModel>
    {
        readonly ShapeRegistry _shapes;

        public BoardModelFactory(ShapeRegistry shapes) => _shapes = shapes;

        public BoardModel Create(LevelDefinition level)
        {
            var pieces = new List<PieceModel>(level.pieces.Count);
            var byId = new Dictionary<string, PieceModel>();

            foreach (var def in level.pieces)
            {
                if (!_shapes.TryGet(def.shape, out var template))
                    throw new ArgumentException($"[{level.name}] Piece '{def.id}' uses unknown shape '{def.shape}'.");

                var piece = new PieceModel(def.id, template, def.parameters, def.Motion, def.position, def.rotation,
                    def.Color, level.tubeRadius);
                pieces.Add(piece);
                byId[def.id] = piece;
            }

            var links = new List<LinkModel>(level.links.Count);
            foreach (var def in level.links)
            {
                if (!byId.TryGetValue(def.targetId ?? string.Empty, out var target))
                    throw new ArgumentException($"[{level.name}] Link '{def.id}' targets unknown piece '{def.targetId}'.");

                PieceModel owner = null;
                if (!def.IsPin && !byId.TryGetValue(def.ownerId ?? string.Empty, out owner))
                    throw new ArgumentException($"[{level.name}] Clip '{def.id}' has unknown owner '{def.ownerId}'.");

                links.Add(new LinkModel(def.id, owner, target, def.point, level.clipHalfSize));
            }

            return new BoardModel(pieces, links);
        }
    }
}
