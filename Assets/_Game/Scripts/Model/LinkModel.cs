using UnityEngine;

namespace RotateRings.Gameplay.Model
{
    /// <summary>
    /// Something gripping Target's tube at a fixed world point: a clip rigidly attached to Owner,
    /// or a pin fixed to the board (Owner is null).
    /// </summary>
    public class LinkModel
    {
        public string Id { get; }
        public PieceModel Owner { get; }
        public PieceModel Target { get; }
        public Vector2 GripPoint { get; }

        /// <summary>Half the clip head's length along the target's tube.</summary>
        public float HalfSize { get; }

        public bool IsPin => Owner == null;

        /// <summary>Set once the owner moves while the clip is free: it no longer grips the target.</summary>
        public bool IsDetached { get; private set; }

        /// <summary>The clip head's position in the owner's local space (it moves rigidly with the owner).</summary>
        public Vector2 HeadOnOwner { get; }

        public LinkModel(string id, PieceModel owner, PieceModel target, Vector2 gripPoint, float halfSize)
        {
            Id = id;
            Owner = owner;
            Target = target;
            GripPoint = gripPoint;
            HalfSize = halfSize;
            HeadOnOwner = owner != null ? owner.WorldToLocal(gripPoint) : gripPoint;
        }

        /// <summary>Current world position of the clip head (pins never move).</summary>
        public Vector2 HeadPosition => Owner != null ? Owner.LocalToWorld(HeadOnOwner) : GripPoint;

        public bool IsEngaged =>
            !IsDetached
            && (Owner == null || !Owner.IsRemoved)
            && !Target.IsRemoved
            && Target.Path.Covers(Target.WorldToLocal(GripPoint), HalfSize, Target.TubeRadius);

        internal void Detach() => IsDetached = true;
    }
}
