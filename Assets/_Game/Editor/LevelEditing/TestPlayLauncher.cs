using System.IO;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Scope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RotateRings.EditorTools.LevelEditing
{
    /// <summary>
    /// Plays the level being edited in the Gameplay scene, then returns to the scene that was open before.
    /// The level is handed over through a temp file whose path is kept in SessionState (see EditorTestLevel).
    /// </summary>
    [InitializeOnLoad]
    public static class TestPlayLauncher
    {
        const string GameplayScenePath = "Assets/_Game/Scenes/Gameplay.unity";
        const string TestLevelFile = "Temp/RotateRings_TestLevel.json";
        const string ReturnSceneKey = "RotateRings.TestReturnScene";

        static TestPlayLauncher() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        public static void Launch(LevelDefinition level)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var path = Path.GetFullPath(TestLevelFile);
            File.WriteAllText(path, LevelJson.ToJson(level));
            SessionState.SetString(EditorTestLevel.SessionKey, path);

            var current = SceneManager.GetActiveScene().path;
            SessionState.SetString(ReturnSceneKey, current);
            if (current != GameplayScenePath)
                EditorSceneManager.OpenScene(GameplayScenePath);

            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode
                || string.IsNullOrEmpty(SessionState.GetString(EditorTestLevel.SessionKey, string.Empty)))
                return;

            SessionState.EraseString(EditorTestLevel.SessionKey);
            var back = SessionState.GetString(ReturnSceneKey, string.Empty);
            SessionState.EraseString(ReturnSceneKey);

            if (!string.IsNullOrEmpty(back) && back != SceneManager.GetActiveScene().path && File.Exists(back))
                EditorSceneManager.OpenScene(back);
        }
    }
}
