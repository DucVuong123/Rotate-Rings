using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Gameplay.Data
{
    /// <summary>
    /// The "rocket" board of the original game (level 35), rebuilt with the editor's pieces.
    /// Coordinates come from the screenshot at 64 px per unit, origin at px (213, 318); bombs are left out.
    ///
    /// How it plays:
    /// - the orange core ring owns five clips (hull arcs, top arc, inner arc, arch) and goes once all are free;
    /// - the top arc can only swing out once a hull arc is gone (it would cross the hull otherwise);
    /// - the tail is a stack of pinned bars and hooks that block each other, freed from the bottom up,
    ///   and the T piece and the side rails can only slide out once the hooks next to them are gone;
    /// - each fin is an arc clipping a short bar; sliding the bar off frees both.
    /// </summary>
    public static class RocketLevel
    {
        const float TubeRadius = 0.08f;

        static readonly Color Cyan = new(0.25f, 0.8f, 0.95f);

        public static LevelDefinition Create()
        {
            var level = new LevelDefinition { name = "Rocket", tubeRadius = TubeRadius };
            BuildBody(level);
            BuildFins(level, side: -1f);
            BuildFins(level, side: 1f);
            BuildTail(level);
            return level;
        }

        // ---------- Body ----------

        static void BuildBody(LevelDefinition level)
        {
            var core = new Vector2(0f, 1.375f);
            const float coreRadius = 0.86f;
            Arc(level, "Core", core, coreRadius, 0f, 360f, SampleLevels.Orange);

            // Inner C opening upward, inside the core.
            const float innerRadius = 0.42f;
            Arc(level, "Inner", core, innerRadius, 90f + 50f, 90f + 310f, SampleLevels.LightBlue);
            SampleLevels.Clip(level, "Core", "Inner", core + Vector2.down * innerRadius);

            // Shallow arc over the core.
            var topCenter = new Vector2(0f, 4.66f);
            Arc(level, "TopArc", topCenter, 2f, 270f - 24f, 270f + 24f, SampleLevels.Purple);
            SampleLevels.Clip(level, "Core", "TopArc", topCenter + Vector2.down * 2f);

            // Arch under the core.
            var archCenter = new Vector2(0f, -1.59f);
            Arc(level, "Arch", archCenter, 1.875f, 90f - 23f, 90f + 23f, SampleLevels.Blue);
            SampleLevels.Clip(level, "Core", "Arch", archCenter + Vector2.up * 1.875f);

            // Hull: two big arcs, mirrored.
            const float hullRadius = 3.125f;
            var leftHull = new Vector2(1.83f, 2f);
            var rightHull = new Vector2(-1.83f, 2f);
            Arc(level, "LeftHull", leftHull, hullRadius, 131.5f, 204.5f, SampleLevels.Red);
            Arc(level, "RightHull", rightHull, hullRadius, -24.5f, 48.5f, SampleLevels.Lime);
            SampleLevels.Clip(level, "Core", "LeftHull", OnCircleAtY(leftHull, hullRadius, core.y, left: true));
            SampleLevels.Clip(level, "Core", "RightHull", OnCircleAtY(rightHull, hullRadius, core.y, left: false));
        }

        // ---------- Fins (side = -1 left, +1 right) ----------

        static void BuildFins(LevelDefinition level, float side)
        {
            var name = side < 0f ? "Left" : "Right";
            // Coordinates below are written for the left fin; the right fin mirrors them.
            Vector2 M(float x, float y) => new(side < 0f ? x : -x, y);
            float MirrorAngle(float degrees) => side < 0f ? degrees : 180f - degrees;

            // Upper fin: an arc whose clip grips a short bar crossing it.
            var finCenter = M(-1.14f, -0.5f);
            var finArc = $"{name}FinArc";
            var finBar = $"{name}FinBar";
            Arc(level, finArc, finCenter, 1.24f, MirrorAngle(side < 0f ? 106f : 165f), MirrorAngle(side < 0f ? 165f : 106f),
                side < 0f ? SampleLevels.Purple : SampleLevels.DarkGreen);
            var barA = M(-2.47f, 0.47f);
            var barB = M(-2.08f, -0.03f);
            Bar(level, finBar, barA, barB, side < 0f ? SampleLevels.Red : Cyan);
            SampleLevels.Clip(level, finArc, finBar, CrossingOnBar(barA, barB, finCenter, 1.24f));

            // Lower wing: an arc under a short bar, clipped by a short stem.
            var wingCenter = M(-1.85f, 0.6f);
            var wingArc = $"{name}WingArc";
            var wingBar = $"{name}WingBar";
            Arc(level, wingArc, wingCenter, 1.325f, MirrorAngle(side < 0f ? -110f : -57f), MirrorAngle(side < 0f ? -57f : -110f),
                side < 0f ? SampleLevels.Lime : SampleLevels.Orange);
            Bar(level, wingBar, M(-2.16f, -0.27f), M(-1.30f, -0.27f), side < 0f ? SampleLevels.Blue : SampleLevels.Purple);
            SampleLevels.Clip(level, wingArc, wingBar, M(-1.73f, -0.27f));
        }

        // ---------- Tail ----------

        static void BuildTail(LevelDefinition level)
        {
            // Side rails, pinned in the middle; they slide down once the hooks under them are gone.
            PinnedBar(level, "LeftRail", new Vector2(-1.016f, 0.4375f), new Vector2(-0.781f, -0.6875f), SampleLevels.DarkGreen);
            PinnedBar(level, "RightRail", new Vector2(1.016f, 0.4375f), new Vector2(0.781f, -0.6875f), Cyan);

            // T: a red bar clipping a lime bar under it; the lime bar slides sideways out of the clip.
            Bar(level, "TBar", new Vector2(-0.59f, -0.89f), new Vector2(0.58f, -0.89f), SampleLevels.Red);
            Bar(level, "TStem", new Vector2(-0.45f, -1.38f), new Vector2(0.45f, -1.38f), SampleLevels.Lime);
            SampleLevels.Clip(level, "TBar", "TStem", new Vector2(0f, -1.38f));

            // Hooks either side of the T.
            Hook(level, "LeftHook", new Vector2(-0.95f, -1.36f), -90f, 0.42f, 0.2f, 90f, 0.12f, SampleLevels.Blue);
            Hook(level, "RightHook", new Vector2(0.95f, -1.36f), -90f, 0.42f, 0.2f, -90f, 0.12f, SampleLevels.DarkGreen);

            // Bottom rows.
            Hook(level, "RedHook", new Vector2(-1.14f, -2.30f), -135f, 0.45f, 0.25f, -45f, 0.12f, SampleLevels.Red);
            Hook(level, "PurpleHook", new Vector2(1.14f, -2.30f), -45f, 0.45f, 0.25f, 45f, 0.12f, SampleLevels.Purple);
            PinnedBar(level, "OrangeRod", new Vector2(-0.2f, -1.72f), new Vector2(-0.39f, -2.84f), SampleLevels.Orange);
            PinnedBar(level, "RedRod", new Vector2(0.2f, -1.72f), new Vector2(0.39f, -2.84f), SampleLevels.Red);
            Hook(level, "BlueHook", new Vector2(-0.86f, -3.125f), -118.5f, 0.46f, 0.2f, 60f, 0.12f, SampleLevels.Blue);
            Hook(level, "GreenHook", new Vector2(0.86f, -3.125f), -61.5f, 0.46f, 0.2f, -60f, 0.12f, SampleLevels.Lime);
            Hook(level, "CyanHook", new Vector2(-0.28f, -4.06f), -98.9f, 0.52f, 0.3f, -90f, 0.15f, Cyan);
            Hook(level, "TealHook", new Vector2(0.28f, -4.06f), -81.1f, 0.52f, 0.3f, 90f, 0.15f, SampleLevels.DarkGreen);
        }

        // ---------- Helpers ----------

        /// <summary>Arc of a circle from startDeg to endDeg (CCW). The arc shape's rotation is its gap direction.</summary>
        static void Arc(LevelDefinition level, string id, Vector2 center, float radius, float startDeg, float endDeg, Color color)
        {
            var sweep = endDeg - startDeg;
            var middle = (startDeg + endDeg) * 0.5f;
            SampleLevels.Piece(level, id, ArcShape.TemplateId, center, Mathf.Approximately(sweep, 360f) ? 0f : middle - 180f,
                MotionType.Rotate, color, new ShapeParams { radius = radius, sweep = sweep });
        }

        static void Bar(LevelDefinition level, string id, Vector2 a, Vector2 b, Color color)
        {
            SampleLevels.Piece(level, id, BarShape.TemplateId, (a + b) * 0.5f, AngleMath.AngleOf(b - a), MotionType.Slide, color,
                new ShapeParams { length = (b - a).magnitude });
        }

        static void PinnedBar(LevelDefinition level, string id, Vector2 a, Vector2 b, Color color)
        {
            Bar(level, id, a, b, color);
            SampleLevels.Pin(level, id, (a + b) * 0.5f);
        }

        /// <summary>
        /// Elbow whose first arm runs toward pivot along heading, then bends by bendAngle into a second arm.
        /// A silver pin sits on the first arm, pinDistance from its free end.
        /// </summary>
        static void Hook(LevelDefinition level, string id, Vector2 pivot, float heading, float armA, float armB,
            float bendAngle, float pinDistance, Color color)
        {
            SampleLevels.Piece(level, id, ElbowShape.TemplateId, pivot, heading, MotionType.Slide, color,
                new ShapeParams { length = armA, length2 = armB, bendRadius = 0.15f, bendAngle = bendAngle });

            var direction = AngleMath.Direction(heading);
            var freeEnd = pivot - direction * armA;
            SampleLevels.Pin(level, id, freeEnd + direction * pinDistance);
        }

        static Vector2 OnCircleAtY(Vector2 center, float radius, float y, bool left)
        {
            var dx = Mathf.Sqrt(radius * radius - (y - center.y) * (y - center.y));
            return new Vector2(center.x + (left ? -dx : dx), y);
        }

        /// <summary>The point where segment a-b crosses the circle (the clip sits right on the crossing).</summary>
        static Vector2 CrossingOnBar(Vector2 a, Vector2 b, Vector2 center, float radius)
        {
            var best = a;
            var bestError = float.MaxValue;
            for (var i = 0; i <= 1000; i++)
            {
                var point = Vector2.Lerp(a, b, i / 1000f);
                var error = Mathf.Abs((point - center).magnitude - radius);
                if (error < bestError)
                {
                    best = point;
                    bestError = error;
                }
            }

            return best;
        }
    }
}
