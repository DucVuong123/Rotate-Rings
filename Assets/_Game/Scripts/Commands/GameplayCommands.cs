using System.Collections.Generic;
using RotateRings.Gameplay.Model;
using VitalRouter;

namespace RotateRings.Gameplay.Commands
{
    /// <summary>One move freed these pieces (several when removals cascade).</summary>
    public readonly struct PiecesRemovedCommand : ICommand
    {
        public readonly IReadOnlyList<PieceModel> Pieces;
        public PiecesRemovedCommand(IReadOnlyList<PieceModel> pieces) => Pieces = pieces;
    }

    /// <summary>The player tried to drag a piece that cannot move (held by its own clips, or Fixed).</summary>
    public readonly struct PieceLockedCommand : ICommand
    {
        public readonly PieceModel Piece;
        public PieceLockedCommand(PieceModel piece) => Piece = piece;
    }

    public readonly struct LevelClearedCommand : ICommand
    {
        public readonly int LevelIndex;
        public readonly int Score;

        public LevelClearedCommand(int levelIndex, int score)
        {
            LevelIndex = levelIndex;
            Score = score;
        }
    }

    public readonly struct LoadLevelCommand : ICommand
    {
        public readonly int LevelIndex;
        public LoadLevelCommand(int levelIndex) => LevelIndex = levelIndex;
    }
}
