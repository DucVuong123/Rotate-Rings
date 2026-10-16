using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.Data
{
    /// <summary>
    /// Boards recreated from the original game (Level 1 tutorial, Level 4, Level 5), plus a demo of the slide pieces.
    /// Ring positions converted from 900x1600 screenshots at 160 px per unit.
    /// </summary>
    public static class SampleLevels
    {
        const float RingRadius = 0.6f;

        public static readonly Color Orange = new(1f, 0.55f, 0.12f);
        public static readonly Color LightBlue = new(0.3f, 0.72f, 0.95f);
        public static readonly Color Purple = new(0.56f, 0.26f, 0.95f);
        public static readonly Color Red = new(0.93f, 0.22f, 0.2f);
        public static readonly Color DarkGreen = new(0.05f, 0.6f, 0.36f);
        public static readonly Color Lime = new(0.47f, 0.8f, 0.16f);
        public static readonly Color Blue = new(0.16f, 0.32f, 0.9f);

        public static readonly Color[] Palette = { Orange, LightBlue, Purple, Red, DarkGreen, Lime, Blue, new(0.98f, 0.8f, 0.15f), new(0.95f, 0.45f, 0.7f), new(0.55f, 0.35f, 0.2f) };

        public static LevelDefinition Level01()
        {
            var level = New("Level 1");
            Ring(level, "A", -0.75f, 0f, 90f, Orange);
            Ring(level, "B", 0.75f, 0f, 270f, LightBlue);
            RingClip(level, "B", "A");
            return level;
        }

        public static LevelDefinition Level04()
        {
            var level = New("Level 4");
            Ring(level, "Orange", -0.53f, 2.59f, 65f, Orange);
            Ring(level, "LightBlue", 0.61f, 1.64f, 23f, LightBlue);
            Ring(level, "Purple", -0.59f, 0.75f, 125f, Purple);
            Ring(level, "Red", -1.45f, -0.39f, 181f, Red);
            Ring(level, "DarkGreen", 1.44f, 0.39f, 0f, DarkGreen);
            Ring(level, "Lime", 0.22f, -0.47f, -33f, Lime);
            Ring(level, "Blue", -0.59f, -1.72f, 233f, Blue);

            RingClip(level, "LightBlue", "Orange");
            RingClip(level, "Purple", "LightBlue");
            RingClip(level, "Red", "Purple");
            RingClip(level, "Purple", "Lime");
            RingClip(level, "DarkGreen", "Lime");
            RingClip(level, "Lime", "Blue");
            return level;
        }

        public static LevelDefinition Level05()
        {
            var level = New("Level 5");
            Ring(level, "Purple", 0f, 0.09f, 0f, Purple, gapWidth: 0f);
            Ring(level, "DarkGreen", 0f, 2.25f, 90f, DarkGreen);
            Ring(level, "LightBlue", -1.04f, 1.18f, 151f, LightBlue);
            Ring(level, "Orange", 1.04f, 1.18f, 30f, Orange);
            Ring(level, "Red", -1.31f, -0.66f, 210f, Red);
            Ring(level, "Lime", 1.31f, -0.63f, -32f, Lime);
            Ring(level, "Blue", 0f, -1.34f, 270f, Blue);

            RingClip(level, "Purple", "LightBlue");
            RingClip(level, "Purple", "Orange");
            RingClip(level, "Purple", "Red");
            RingClip(level, "Purple", "Lime");
            RingClip(level, "DarkGreen", "LightBlue");
            RingClip(level, "DarkGreen", "Orange");
            RingClip(level, "Red", "Blue");
            RingClip(level, "Lime", "Blue");
            return level;
        }

        /// <summary>
        /// Slide pieces: a bar through two pins holds a ring by its clip; an elbow sits in one pin and is
        /// blocked by its bend in one direction.
        /// </summary>
        public static LevelDefinition SlideDemo()
        {
            var level = New("Slide Demo");

            Piece(level, "Bar", BarShape.TemplateId, new Vector2(0f, 1.6f), 0f, MotionType.Slide, Blue,
                new ShapeParams { length = 2.4f });
            // Both pins left of the clip's stem: sliding right, the stem moves away from them.
            Pin(level, "Bar", new Vector2(-0.9f, 1.6f));
            Pin(level, "Bar", new Vector2(-0.3f, 1.6f));

            Ring(level, "Ring", 0f, 0.3f, -90f, Orange);
            Clip(level, "Bar", "Ring", new Vector2(0f, 0.3f + RingRadius));

            Piece(level, "Elbow", ElbowShape.TemplateId, new Vector2(0.8f, -1.4f), 180f, MotionType.Slide, Lime,
                new ShapeParams { length = 1.2f, length2 = 0.6f, bendRadius = 0.25f, bendAngle = -90f });
            Pin(level, "Elbow", new Vector2(1.4f, -1.4f));
            return level;
        }

        static LevelDefinition New(string name) => new() { name = name };

        public static PieceDef Piece(LevelDefinition level, string id, string shape, Vector2 position, float rotation,
            MotionType motion, Color color, ShapeParams parameters)
        {
            var piece = new PieceDef { id = id, shape = shape, position = position, rotation = rotation, parameters = parameters };
            piece.Motion = motion;
            piece.Color = color;
            level.pieces.Add(piece);
            return piece;
        }

        /// <summary>A C-shaped ring whose gap is gapWidth degrees wide and centered on gapCenter.</summary>
        static void Ring(LevelDefinition level, string id, float x, float y, float gapCenter, Color color, float gapWidth = 100f)
        {
            Piece(level, id, ArcShape.TemplateId, new Vector2(x, y), gapCenter, MotionType.Rotate, color,
                new ShapeParams { radius = RingRadius, sweep = 360f - gapWidth });
        }

        /// <summary>Clip between two rings, gripping the target where the line between centers crosses it.</summary>
        static void RingClip(LevelDefinition level, string owner, string target)
        {
            var o = level.FindPiece(owner).position;
            var t = level.FindPiece(target).position;
            Clip(level, owner, target, t + (o - t).normalized * RingRadius);
        }

        public static void Clip(LevelDefinition level, string owner, string target, Vector2 point)
        {
            level.links.Add(new LinkDef
            {
                id = $"{owner}>{target}", kind = LinkDef.ClipKind, ownerId = owner, targetId = target, point = point,
            });
        }

        public static void Pin(LevelDefinition level, string target, Vector2 point)
        {
            level.links.Add(new LinkDef
            {
                id = $"pin{level.links.Count + 1}", kind = LinkDef.PinKind, ownerId = string.Empty, targetId = target, point = point,
            });
        }
    }
}
