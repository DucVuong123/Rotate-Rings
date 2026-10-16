using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using UnityEngine;

namespace RotateRings.Tests
{
    public class BoardModelTests
    {
        readonly List<PieceModel> _removed = new();

        [TearDown]
        public void TearDown() => _removed.Clear();

        static BoardModel Build(LevelDefinition level) =>
            new BoardModelFactory(ShapeRegistry.CreateDefault()).Create(LevelJson.Clone(level));

        static PieceModel Piece(BoardModel board, string id) => board.FindPiece(id);

        static LinkModel Clip(BoardModel board, string owner, string target) =>
            board.Links.Single(l => l.Owner?.Id == owner && l.Target.Id == target);

        /// <summary>Drags a piece without letting go: nothing breaks yet.</summary>
        static void Drag(BoardModel board, string id, float delta)
        {
            Assert.IsTrue(board.TryMove(Piece(board, id), delta), $"{id} should be movable");
        }

        /// <summary>The player lets go: pieces left with no engaged link break.</summary>
        void Release(BoardModel board) => board.ResolveRemovals(_removed);

        /// <summary>One whole gesture: drag, then release.</summary>
        void Move(BoardModel board, string id, float delta)
        {
            Drag(board, id, delta);
            Release(board);
        }

        // ---------- Rings (ported from the first version) ----------

        [Test]
        public void SampleLevels_StartWithEveryLinkEngaged()
        {
            foreach (var level in new[] { SampleLevels.Level01(), SampleLevels.Level04(), SampleLevels.Level05(), SampleLevels.SlideDemo() })
            {
                var board = Build(level);
                Assert.IsTrue(board.Links.All(l => l.IsEngaged), level.name);
                Assert.IsTrue(board.Pieces.All(board.HasEngagement), level.name);
            }
        }

        [Test]
        public void JsonRoundTrip_KeepsLevel()
        {
            var level = SampleLevels.SlideDemo();
            var copy = LevelJson.Parse(LevelJson.ToJson(level));

            Assert.AreEqual(level.pieces.Count, copy.pieces.Count);
            Assert.AreEqual(level.links.Count, copy.links.Count);
            Assert.AreEqual(MotionType.Slide, copy.FindPiece("Elbow").Motion);
            Assert.AreEqual(-90f, copy.FindPiece("Elbow").parameters.bendAngle);
            Assert.IsTrue(copy.links.Any(l => l.IsPin));
        }

        [Test]
        public void PieceOwningEngagedClip_IsLocked()
        {
            var board = Build(SampleLevels.Level04());

            Assert.IsFalse(board.CanMove(Piece(board, "Purple")));
            Assert.IsFalse(board.CanMove(Piece(board, "DarkGreen")));
            Assert.IsFalse(board.CanMove(Piece(board, "Red")));
            Assert.IsFalse(board.CanMove(Piece(board, "LightBlue")));
            Assert.IsTrue(board.CanMove(Piece(board, "Orange")));
            Assert.IsTrue(board.CanMove(Piece(board, "Blue")));

            var purple = Piece(board, "Purple");
            Assert.IsFalse(board.TryMove(purple, 45f));
            Assert.AreEqual(0f, purple.Offset.Value);
        }

        [Test]
        public void GapOverOnlyClip_RemovesRing_AndUnlocksOwner()
        {
            var board = Build(SampleLevels.Level04());

            Move(board, "Orange", -105f);

            CollectionAssert.AreEqual(new[] { "Orange" }, _removed.Select(r => r.Id));
            Assert.IsTrue(board.CanMove(Piece(board, "LightBlue")));
        }

        [Test]
        public void ClipReleases_ThenGripsAgain_WhenBodyPassesBack()
        {
            var board = Build(SampleLevels.Level05());
            var darkGreenClip = Clip(board, "DarkGreen", "LightBlue");
            var purpleClip = Clip(board, "Purple", "LightBlue");

            Move(board, "LightBlue", -105f);
            Assert.IsFalse(darkGreenClip.IsEngaged);
            Assert.IsTrue(purpleClip.IsEngaged);
            Assert.IsEmpty(_removed);

            Move(board, "LightBlue", -55f);
            Assert.IsTrue(darkGreenClip.IsEngaged);
            Assert.IsFalse(purpleClip.IsEngaged);
            Assert.IsEmpty(_removed);
        }

        [Test]
        public void OwnerIsRemoved_WhenAllItsClipsReleaseTogether()
        {
            var board = Build(SampleLevels.Level05());

            Move(board, "LightBlue", -105f);
            Move(board, "Orange", 104f);

            CollectionAssert.AreEqual(new[] { "DarkGreen" }, _removed.Select(r => r.Id));
        }

        [Test]
        public void LastLinkReleased_RemovesBothRings_AndClearsBoard()
        {
            var board = Build(SampleLevels.Level01());

            Move(board, "A", -90f);

            Assert.AreEqual(2, _removed.Count);
            Assert.IsTrue(board.IsCleared);
        }

        [Test]
        public void Breaking_HappensOnlyWhenThePlayerLetsGo()
        {
            var board = Build(SampleLevels.Level01());

            Drag(board, "A", -90f);
            Assert.IsEmpty(_removed, "still dragging: the gap is over the clip but nothing breaks yet");
            Assert.IsFalse(board.IsCleared);

            Release(board);
            Assert.AreEqual(2, _removed.Count);
            Assert.IsTrue(board.IsCleared);
        }

        [Test]
        public void DraggingThroughTheGap_AndLettingGoElsewhere_DoesNotBreak()
        {
            var board = Build(SampleLevels.Level01());

            // The gap passes under the clip halfway (at -90°) but the ring is released at -180°, gripped again.
            Move(board, "A", -180f);

            Assert.IsEmpty(_removed);
            Assert.IsFalse(Piece(board, "A").IsRemoved);
            Assert.IsTrue(board.Links.All(l => l.IsEngaged));
        }

        [Test]
        public void OwnerMovement_DetachesItsFreeClips()
        {
            var board = Build(SampleLevels.Level05());

            Move(board, "Blue", -117f);
            var redClip = Clip(board, "Red", "Blue");
            Assert.IsFalse(redClip.IsEngaged);
            Assert.IsTrue(board.CanMove(Piece(board, "Red")));

            Move(board, "Red", 10f);
            Move(board, "Blue", 117f);

            Assert.IsTrue(redClip.IsDetached);
            Assert.IsFalse(redClip.IsEngaged);
        }

        [Test]
        public void Level04_FullSolution_ClearsBoard()
        {
            var board = Build(SampleLevels.Level04());

            Move(board, "Orange", -105f);
            Move(board, "Blue", -176f);
            Move(board, "Lime", 68f);
            Move(board, "Lime", 88f);
            Move(board, "LightBlue", -166f);
            Move(board, "Purple", 108f);

            CollectionAssert.AreEquivalent(
                new[] { "Orange", "Blue", "DarkGreen", "Lime", "LightBlue", "Purple", "Red" },
                _removed.Select(r => r.Id));
            Assert.IsTrue(board.IsCleared);
        }

        // ---------- New shapes ----------

        [Test]
        public void Bar_IsLockedByItsClip_ThenSlidesOutOfBothPins()
        {
            var board = Build(SampleLevels.SlideDemo());
            var bar = Piece(board, "Bar");

            Assert.IsFalse(board.CanMove(bar), "the bar's clip still holds the ring");

            Move(board, "Ring", 180f);
            CollectionAssert.AreEqual(new[] { "Ring" }, _removed.Select(p => p.Id));
            Assert.IsTrue(board.CanMove(bar));

            Move(board, "Bar", 0.5f);
            Assert.IsFalse(bar.IsRemoved, "out of the first pin, still inside the second");

            Drag(board, "Bar", 1f);
            Assert.IsFalse(bar.IsRemoved, "out of both pins, but still held by the player");

            Release(board);
            Assert.IsTrue(bar.IsRemoved);
        }

        [Test]
        public void Elbow_SlidesOutThroughItsFreeEnd()
        {
            var board = Build(SampleLevels.SlideDemo());

            Move(board, "Elbow", 0.8f);

            Assert.IsTrue(Piece(board, "Elbow").IsRemoved);
        }

        [Test]
        public void Elbow_IsBlockedWhenThePinReachesTheBend()
        {
            var board = Build(SampleLevels.SlideDemo());
            var elbow = Piece(board, "Elbow");

            Move(board, "Elbow", -2f);

            Assert.IsFalse(elbow.IsRemoved);
            Assert.AreEqual(-0.6f, elbow.Offset.Value, 0.11f, "stops with the pin at the joint");
            Assert.IsTrue(board.HasEngagement(elbow));
        }

        [Test]
        public void LongArc_ReleasesAtItsEnd()
        {
            var level = new LevelDefinition { name = "arc" };
            SampleLevels.Piece(level, "Arc", ArcShape.TemplateId, Vector2.zero, 0f, MotionType.Rotate, Color.red,
                new ShapeParams { radius = 3f, sweep = 60f });
            // The body spans 150°..210° around the pivot; pin it at 180°.
            SampleLevels.Pin(level, "Arc", new Vector2(-3f, 0f));
            var board = Build(level);

            Move(board, "Arc", 20f);
            Assert.IsFalse(Piece(board, "Arc").IsRemoved);

            Move(board, "Arc", 20f);
            Assert.IsTrue(Piece(board, "Arc").IsRemoved);
        }

        // ---------- Rocket ----------

        [Test]
        public void Rocket_HasNoProblems()
        {
            var issues = LevelValidator.Validate(RocketLevel.Create(), ShapeRegistry.CreateDefault());
            Assert.IsEmpty(issues, string.Join("\n", issues.Select(i => i.Message)));
        }

        [Test]
        public void Rocket_FullSolution_ClearsBoard()
        {
            var board = Build(RocketLevel.Create());

            void Free(string id, float delta, params string[] expected)
            {
                var before = _removed.Count;
                Move(board, id, delta);
                var gone = _removed.Skip(before).Select(p => p.Id).ToArray();
                CollectionAssert.AreEquivalent(expected.Length == 0 ? new[] { id } : expected, gone,
                    $"after moving {id} by {delta} (now at {Piece(board, id).Offset.Value:0.00})");
            }

            // The tail, from the bottom up.
            Free("RedHook", 0.3f);
            Free("PurpleHook", 0.3f);
            Free("CyanHook", 0.35f);
            Free("TealHook", 0.35f);
            Free("BlueHook", 0.3f);
            Free("GreenHook", 0.3f);
            Free("OrangeRod", 0.75f);
            Free("RedRod", 0.75f);
            Free("LeftHook", 0.3f);
            Free("RightHook", 0.3f);
            Free("TStem", -0.65f, "TStem", "TBar");
            Free("LeftRail", 0.75f);
            Free("RightRail", 0.75f);

            // Fins: slide each bar off its arc's clip.
            Free("LeftFinBar", -0.35f, "LeftFinBar", "LeftFinArc");
            Free("RightFinBar", -0.35f, "RightFinBar", "RightFinArc");
            Free("LeftWingBar", -0.6f, "LeftWingBar", "LeftWingArc");
            Free("RightWingBar", -0.6f, "RightWingBar", "RightWingArc");

            // Body: the core's clips one by one; the top arc needs a hull out of the way first.
            Free("Inner", 180f);
            Free("Arch", 30f);
            Free("LeftHull", -15f);
            Free("RightHull", 15f);
            Free("TopArc", 30f, "TopArc", "Core");

            Assert.IsTrue(board.IsCleared);
        }

        [Test]
        public void Rocket_TopArc_IsBlockedByTheHulls()
        {
            var board = Build(RocketLevel.Create());

            Move(board, "TopArc", 30f);
            Assert.IsFalse(Piece(board, "TopArc").IsRemoved);
            Move(board, "TopArc", -60f);
            Assert.IsFalse(Piece(board, "TopArc").IsRemoved);
        }

        // ---------- Collisions ----------

        static void Ring(LevelDefinition level, string id, float x, float y, float gapDirection, float gapWidth = 100f) =>
            SampleLevels.Piece(level, id, ArcShape.TemplateId, new Vector2(x, y), gapDirection, MotionType.Rotate, Color.red,
                new ShapeParams { radius = 0.6f, sweep = 360f - gapWidth });

        [Test]
        public void SwingingClip_IsStoppedByAnotherRing()
        {
            // A owns a clip on B. Once B is gone, A's dangling stem and head swing with A and run into C above.
            var level = new LevelDefinition { name = "swing" };
            Ring(level, "A", 0f, 0f, -90f);
            Ring(level, "B", 1.5f, 0f, 0f);
            Ring(level, "C", 0f, 1.5f, 0f);
            SampleLevels.Clip(level, "A", "B", new Vector2(0.9f, 0f));
            SampleLevels.Pin(level, "A", new Vector2(-0.6f, 0f));
            SampleLevels.Pin(level, "C", new Vector2(0f, 2.1f));
            var board = Build(level);

            Move(board, "B", 180f);
            CollectionAssert.AreEqual(new[] { "B" }, _removed.Select(p => p.Id));

            Move(board, "A", 90f);
            var angle = Piece(board, "A").Offset.Value;
            Assert.Less(angle, 70f, "the head should hit C's tube before reaching it");
            Assert.Greater(angle, 45f, "but only near C, not right away");

            // It stops touching C: not even a tenth of a degree more is possible.
            Move(board, "A", 0.1f);
            Assert.AreEqual(angle, Piece(board, "A").Offset.Value, 0.01f);

            // Swinging the other way is free.
            Move(board, "A", -150f);
            Assert.AreEqual(angle - 150f, Piece(board, "A").Offset.Value, 0.01f);
        }

        [Test]
        public void ReleasedClipHead_CannotSlipSidewaysThroughTheGapEdges()
        {
            // T's narrow gap faces O, so O's clip head sits loose in it. Turning O would push the head along T's
            // tube into the gap's edges.
            var level = new LevelDefinition { name = "track" };
            Ring(level, "T", 0f, 0f, 0f, gapWidth: 40f);
            Ring(level, "O", 1.5f, 0f, 90f);
            SampleLevels.Clip(level, "O", "T", new Vector2(0.6f, 0f));
            SampleLevels.Pin(level, "T", new Vector2(-0.6f, 0f));
            SampleLevels.Pin(level, "O", new Vector2(2.1f, 0f));
            var board = Build(level);

            Assert.IsFalse(board.Links.Single(l => l.Owner?.Id == "O").IsEngaged);
            Assert.IsTrue(board.CanMove(Piece(board, "O")));

            Move(board, "O", 30f);
            Assert.Less(Piece(board, "O").Offset.Value, 12f);

            Move(board, "O", -60f);
            Assert.Greater(Piece(board, "O").Offset.Value, -12f);
        }

        [Test]
        public void RotatingBar_IsBlockedByItsPin()
        {
            var level = new LevelDefinition { name = "rotating bar" };
            SampleLevels.Piece(level, "Bar", BarShape.TemplateId, Vector2.zero, 0f, MotionType.Rotate, Color.red,
                new ShapeParams { length = 2f });
            SampleLevels.Pin(level, "Bar", new Vector2(0.5f, 0f));
            var board = Build(level);

            Move(board, "Bar", 30f);

            Assert.AreEqual(0f, Piece(board, "Bar").Offset.Value);
            Assert.IsFalse(Piece(board, "Bar").IsRemoved);
        }
    }
}
