using System.Collections.Generic;
using UnityEngine;

namespace RotateRings.Gameplay.Data
{
    /// <summary>Ordered list of level JSON files played by the Gameplay scene.</summary>
    [CreateAssetMenu(menuName = "Rotate Rings/Level Catalog", fileName = "LevelCatalog")]
    public class LevelCatalog : ScriptableObject
    {
        public List<TextAsset> levels = new();

        public int Count => levels.Count;

        public LevelDefinition Get(int index) => LevelJson.Parse(levels[(index % Count + Count) % Count].text);
    }
}
