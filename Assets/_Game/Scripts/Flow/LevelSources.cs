using RotateRings.Gameplay.Data;

namespace RotateRings.Gameplay.Flow
{
    /// <summary>Where the gameplay session gets its levels from.</summary>
    public interface ILevelSource
    {
        int Count { get; }
        LevelDefinition Get(int index);
    }

    /// <summary>The Gameplay scene: levels in catalog order.</summary>
    public class CatalogLevelSource : ILevelSource
    {
        readonly LevelCatalog _catalog;

        public CatalogLevelSource(LevelCatalog catalog) => _catalog = catalog;

        public int Count => _catalog.Count;
        public LevelDefinition Get(int index) => _catalog.Get(index);
    }

    /// <summary>The editor's Test Play: a copy of the level being edited.</summary>
    public class SingleLevelSource : ILevelSource
    {
        readonly LevelDefinition _level;

        public SingleLevelSource(LevelDefinition level) => _level = LevelJson.Clone(level);

        public int Count => 1;
        public LevelDefinition Get(int index) => LevelJson.Clone(_level);
    }
}
