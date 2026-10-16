using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Flow;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace RotateRings.Gameplay.Scope
{
    public class GameplayLifetimeScope : LifetimeScope
    {
        [SerializeField] LevelCatalog levelCatalog;
        [SerializeField] GameplayViewRefs view;
        [SerializeField] GameplayUiRefs ui;

        protected override void Configure(IContainerBuilder builder)
        {
            new GameplayViewInstaller(view).Install(builder);
            new GameplaySessionInstaller(ui, EditorTestLevel.TryLoad() ?? new CatalogLevelSource(levelCatalog)).Install(builder);
        }
    }

    /// <summary>
    /// "Test Play" from the edit-mode level editor: it writes the level to a temp file and records the path in
    /// SessionState, and the Gameplay scene plays that one level instead of the catalog.
    /// </summary>
    public static class EditorTestLevel
    {
        public const string SessionKey = "RotateRings.TestLevelPath";

        public static ILevelSource TryLoad()
        {
#if UNITY_EDITOR
            var path = UnityEditor.SessionState.GetString(SessionKey, string.Empty);
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                return new SingleLevelSource(LevelJson.Parse(System.IO.File.ReadAllText(path)));
#endif
            return null;
        }
    }
}
