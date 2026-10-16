using System.IO;
using RotateRings.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace RotateRings.EditorTools
{
    public static class SampleLevelGenerator
    {
        public const string LevelsFolder = "Assets/_Game/Levels";
        public const string CatalogPath = LevelsFolder + "/LevelCatalog.asset";

        /// <summary>Writes the sample levels as JSON (keeping files the level editor saved) and fills the catalog.</summary>
        [MenuItem("Tools/Rotate Rings/Generate Sample Levels")]
        public static LevelCatalog Generate()
        {
            EditorFolders.Ensure(LevelsFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var samples = new[]
            {
                ("level_001", SampleLevels.Level01()),
                ("level_004", SampleLevels.Level04()),
                ("level_005", SampleLevels.Level05()),
                ("slide_demo", SampleLevels.SlideDemo()),
                ("rocket", RocketLevel.Create()),
            };

            catalog.levels.RemoveAll(asset => asset == null);
            foreach (var (fileName, level) in samples)
            {
                var assetPath = $"{LevelsFolder}/{fileName}.json";
                File.WriteAllText(Path.GetFullPath(assetPath), LevelJson.ToJson(level));
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (!catalog.levels.Contains(asset))
                    catalog.levels.Add(asset);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Rotate Rings] {samples.Length} sample levels written to {LevelsFolder}; catalog has {catalog.levels.Count}.");
            return catalog;
        }
    }

    static class EditorFolders
    {
        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
