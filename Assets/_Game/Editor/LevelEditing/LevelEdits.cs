using System.Linq;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.EditorTools.LevelEditing
{
    /// <summary>Editing operations on a LevelDefinition that keep pieces and links consistent.</summary>
    public static class LevelEdits
    {
        /// <summary>A safe file name (lowercase, letters, digits and underscores) for a level name.</summary>
        public static string FileNameFor(string levelName)
        {
            var name = new string((levelName ?? "level").Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            return string.IsNullOrEmpty(name) ? "level" : name;
        }

        public static string UniqueId(LevelDefinition level, string prefix)
        {
            for (var i = 1; ; i++)
            {
                var id = $"{prefix}{i}";
                if (level.FindPiece(id) == null && level.FindLink(id) == null)
                    return id;
            }
        }

        public static PieceDef AddPiece(LevelDefinition level, IShapeTemplate template, Vector2 position)
        {
            var piece = new PieceDef
            {
                id = UniqueId(level, template.Id),
                shape = template.Id,
                position = position,
                rotation = 0f,
                parameters = template.DefaultParams,
            };
            piece.Motion = template.DefaultMotion;
            piece.Color = SampleLevels.Palette[level.pieces.Count % SampleLevels.Palette.Length];
            level.pieces.Add(piece);
            return piece;
        }

        public static LinkDef AddLink(LevelDefinition level, string kind, string ownerId, string targetId, Vector2 point)
        {
            var link = new LinkDef
            {
                id = UniqueId(level, kind),
                kind = kind,
                ownerId = kind == LinkDef.PinKind ? string.Empty : ownerId,
                targetId = targetId,
                point = point,
            };
            level.links.Add(link);
            return link;
        }

        public static void DeletePiece(LevelDefinition level, string id)
        {
            level.pieces.RemoveAll(p => p.id == id);
            level.links.RemoveAll(l => l.targetId == id || l.ownerId == id);
        }

        public static void DeleteLink(LevelDefinition level, string id) => level.links.RemoveAll(l => l.id == id);

        public static PieceDef Duplicate(LevelDefinition level, string id, Vector2 offset)
        {
            var source = level.FindPiece(id);
            if (source == null)
                return null;

            var copy = JsonUtility.FromJson<PieceDef>(JsonUtility.ToJson(source));
            copy.id = UniqueId(level, source.shape);
            copy.position += offset;
            level.pieces.Add(copy);
            return copy;
        }

        /// <summary>Renames a piece and every link that refers to it. Returns false if the id is taken or empty.</summary>
        public static bool RenamePiece(LevelDefinition level, string oldId, string newId)
        {
            if (string.IsNullOrWhiteSpace(newId) || newId == oldId || level.FindPiece(newId) != null)
                return false;

            level.FindPiece(oldId).id = newId;
            foreach (var link in level.links)
            {
                if (link.ownerId == oldId) link.ownerId = newId;
                if (link.targetId == oldId) link.targetId = newId;
            }

            return true;
        }

        public static bool RenameLink(LevelDefinition level, string oldId, string newId)
        {
            if (string.IsNullOrWhiteSpace(newId) || newId == oldId || level.FindLink(newId) != null)
                return false;

            level.FindLink(oldId).id = newId;
            return true;
        }

        /// <summary>Moves a piece; links gripping it move along so they keep their place on its tube.</summary>
        public static void MovePiece(LevelDefinition level, string id, Vector2 newPosition)
        {
            var piece = level.FindPiece(id);
            var delta = newPosition - piece.position;
            piece.position = newPosition;

            foreach (var link in LinksGripping(level, id))
                link.point += delta;
        }

        /// <summary>Rotates a piece around its pivot; links gripping it turn with it.</summary>
        public static void RotatePiece(LevelDefinition level, string id, float newRotation)
        {
            var piece = level.FindPiece(id);
            var delta = newRotation - piece.rotation;
            piece.rotation = newRotation;

            foreach (var link in LinksGripping(level, id))
                link.point = piece.position + PieceModel.Rotate(link.point - piece.position, delta);
        }

        /// <summary>Changes shape parameters and keeps the links gripping the piece on the new path.</summary>
        public static void SetParameters(LevelDefinition level, ShapeRegistry shapes, string id, ShapeParams parameters)
        {
            var piece = level.FindPiece(id);
            if (!shapes.TryGet(piece.shape, out var template))
            {
                piece.parameters = parameters;
                return;
            }

            piece.parameters = parameters;

            // Grips still on the new tube stay put; the others move to the closest point of it.
            foreach (var link in level.links.Where(l => l.targetId == id))
                link.point = SnapToPath(piece, template, link.point);
        }

        /// <summary>Puts a link's grip on the closest point of its target's tube.</summary>
        public static Vector2 SnapToPath(PieceDef piece, IShapeTemplate template, Vector2 world)
        {
            var path = template.Build(piece.parameters);
            var local = PieceModel.Rotate(world - piece.position, -piece.rotation);
            var onPath = path.PointAt(path.Project(local).S);
            return piece.position + PieceModel.Rotate(onPath, piece.rotation);
        }

        static System.Collections.Generic.IEnumerable<LinkDef> LinksGripping(LevelDefinition level, string id) =>
            level.links.Where(l => l.targetId == id);

        /// <summary>A copy with broken pieces and links removed, so a half-edited level can still be drawn.</summary>
        public static LevelDefinition Sanitized(LevelDefinition level, ShapeRegistry shapes)
        {
            var copy = LevelJson.Clone(level);
            var seen = new System.Collections.Generic.HashSet<string>();
            copy.pieces.RemoveAll(p => string.IsNullOrEmpty(p.id) || !seen.Add(p.id) || !shapes.TryGet(p.shape, out _));
            copy.links.RemoveAll(l =>
                copy.FindPiece(l.targetId) == null
                || !l.IsPin && (copy.FindPiece(l.ownerId) == null || l.ownerId == l.targetId));
            return copy;
        }
    }
}
