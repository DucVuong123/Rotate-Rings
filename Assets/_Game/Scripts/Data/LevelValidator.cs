using System;
using System.Collections.Generic;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;

namespace RotateRings.Gameplay.Data
{
    public enum IssueSeverity
    {
        Warning,
        Error,
    }

    public readonly struct LevelIssue
    {
        public readonly IssueSeverity Severity;
        public readonly string Message;
        public readonly string PieceId;
        public readonly string LinkId;

        public LevelIssue(IssueSeverity severity, string message, string pieceId = null, string linkId = null)
        {
            Severity = severity;
            Message = message;
            PieceId = pieceId;
            LinkId = linkId;
        }
    }

    /// <summary>Problems a designer should fix before a level is playable as intended.</summary>
    public static class LevelValidator
    {
        public static List<LevelIssue> Validate(LevelDefinition level, ShapeRegistry shapes)
        {
            var issues = new List<LevelIssue>();
            var pieceIds = new HashSet<string>();
            var structurallyValid = true;

            foreach (var piece in level.pieces)
            {
                if (string.IsNullOrWhiteSpace(piece.id) || !pieceIds.Add(piece.id))
                {
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"Piece id '{piece.id}' is empty or duplicated.", piece.id));
                    structurallyValid = false;
                }

                if (!shapes.TryGet(piece.shape, out _))
                {
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"Piece '{piece.id}' uses unknown shape '{piece.shape}'.", piece.id));
                    structurallyValid = false;
                }
            }

            var linkIds = new HashSet<string>();
            foreach (var link in level.links)
            {
                if (string.IsNullOrWhiteSpace(link.id) || !linkIds.Add(link.id))
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"Link id '{link.id}' is empty or duplicated.", linkId: link.id));

                if (!pieceIds.Contains(link.targetId ?? string.Empty))
                {
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"Link '{link.id}' targets a missing piece.", linkId: link.id));
                    structurallyValid = false;
                }

                if (!link.IsPin && !pieceIds.Contains(link.ownerId ?? string.Empty))
                {
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"Clip '{link.id}' has no owner piece.", linkId: link.id));
                    structurallyValid = false;
                }

                if (!link.IsPin && link.ownerId == link.targetId)
                    issues.Add(new LevelIssue(IssueSeverity.Error, $"Clip '{link.id}' grips its own owner.", link.ownerId, link.id));
            }

            if (!structurallyValid)
                return issues;

            BoardModel board;
            try
            {
                board = new BoardModelFactory(shapes).Create(level);
            }
            catch (Exception e)
            {
                issues.Add(new LevelIssue(IssueSeverity.Error, e.Message));
                return issues;
            }

            using (board)
            {
                foreach (var link in board.Links)
                {
                    var distance = link.Target.DistanceToBody(link.GripPoint);
                    if (distance > link.Target.TubeRadius)
                        issues.Add(new LevelIssue(IssueSeverity.Error,
                            $"Link '{link.Id}' does not touch '{link.Target.Id}' ({distance:0.00} away).", link.Target.Id, link.Id));
                    else if (!link.IsEngaged)
                        issues.Add(new LevelIssue(IssueSeverity.Warning,
                            $"Link '{link.Id}' starts released (in a gap or past an end of '{link.Target.Id}').", link.Target.Id, link.Id));
                }

                foreach (var piece in board.Pieces)
                {
                    if (!board.HasEngagement(piece))
                        issues.Add(new LevelIssue(IssueSeverity.Warning,
                            $"Piece '{piece.Id}' is not held by anything and will vanish at start.", piece.Id));
                }
            }

            return issues;
        }
    }
}
