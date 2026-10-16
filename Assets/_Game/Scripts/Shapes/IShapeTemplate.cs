using System.Collections.Generic;
using UnityEngine;

namespace RotateRings.Gameplay.Shapes
{
    public enum MotionType
    {
        /// <summary>Turns around its pivot.</summary>
        Rotate,

        /// <summary>Slides along its slide axis.</summary>
        Slide,

        /// <summary>Never moves.</summary>
        Fixed,
    }

    /// <summary>
    /// A kind of piece (arc, bar, elbow...). Adding a new shape means adding one implementation and
    /// registering it in ShapeRegistry; the board rules only see the resulting PiecePath.
    /// </summary>
    public interface IShapeTemplate
    {
        string Id { get; }
        string DisplayName { get; }
        ShapeParams DefaultParams { get; }
        MotionType DefaultMotion { get; }
        IReadOnlyList<ParamDef> Params { get; }
        IReadOnlyList<HandleDef> Handles { get; }

        /// <summary>Centerline in local space; the pivot is the local origin.</summary>
        PiecePath Build(ShapeParams parameters);

        /// <summary>Unit slide direction in local space (used when the piece's motion is Slide).</summary>
        Vector2 SlideAxis(ShapeParams parameters);
    }
}
