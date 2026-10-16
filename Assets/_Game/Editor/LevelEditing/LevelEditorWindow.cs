using System.Collections.Generic;
using System.IO;
using System.Linq;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Shapes;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace RotateRings.EditorTools.LevelEditing
{
    /// <summary>
    /// Edit-mode level editor: lay out pieces directly in the Scene view without entering Play Mode.
    /// The window holds file actions, tools, snapping, an inspector for the selection, the problems list and
    /// Test Play. Edits go through LevelEditSession so Unity's Undo/Redo covers them.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        const string LevelsFolder = "Assets/_Game/Levels";
        const string GridPref = "RotateRings.LevelEditor.Grid";
        const string AnglePref = "RotateRings.LevelEditor.Angle";

        static readonly float[] GridSteps = { 0f, 0.05f, 0.1f, 0.25f, 0.5f };
        static readonly float[] AngleSteps = { 0f, 5f, 15f, 45f };

        [SerializeField] LevelEditSession session;

        ShapeRegistry _shapes;
        SceneViewLevelPreview _preview;
        SceneViewLevelTools _tools;
        Vector2 _scroll;
        string _status = "Ready.";
        List<LevelIssue> _issues = new();

        [MenuItem("Tools/Rotate Rings/Level Editor %#l")]
        public static LevelEditorWindow Open()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(300f, 400f);
            window.Show();
            return window;
        }

        /// <summary>Double-clicking a level JSON in the Project window opens it here.</summary>
        [OnOpenAsset]
        static bool OnOpenAsset(int instanceId, int line)
        {
            var path = AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(instanceId));
            if (!path.StartsWith(LevelsFolder) || !path.EndsWith(".json"))
                return false;

            Open().LoadFile(path);
            return true;
        }

        void OnEnable()
        {
            _shapes = ShapeRegistry.CreateDefault();
            if (session == null)
                session = LevelEditSession.Create(new LevelDefinition { name = "New Level" });

            _preview = new SceneViewLevelPreview(_shapes);
            _tools = new SceneViewLevelTools(() => session, () => _preview, _shapes, SetStatus, FrameLevel)
            {
                GridSnap = EditorPrefs.GetFloat(GridPref, 0.05f),
                AngleSnap = EditorPrefs.GetFloat(AnglePref, 5f),
            };
            _tools.ToolChanged += Repaint;

            session.Changed += OnSessionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Refresh();
        }

        void OnDisable()
        {
            if (session != null)
                session.Changed -= OnSessionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
            _preview?.Dispose();
            _preview = null;
        }

        void OnDestroy()
        {
            if (session != null)
                DestroyImmediate(session);
        }

        // ---------- Sync ----------

        void OnSessionChanged() => Refresh();

        void OnUndoRedo() => session.ReloadFromJson();

        void OnSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to) => Refresh();

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                _preview?.Clear();
            else if (change == PlayModeStateChange.EnteredEditMode)
                Refresh();
        }

        void Refresh()
        {
            if (_preview == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _preview.Rebuild(session.Level, session.SelectedPiece);
            _issues = LevelValidator.Validate(session.Level, _shapes);
            SceneView.RepaintAll();
            Repaint();
        }

        void OnSceneGUI(SceneView sceneView)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _tools.OnSceneGUI(sceneView);
        }

        void SetStatus(string message)
        {
            _status = message;
            Repaint();
        }

        // ---------- GUI ----------

        void OnGUI()
        {
            if (session == null)
                return;

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Playing. Stop Play Mode to keep editing the level.", MessageType.Info);
                return;
            }

            if (!_preview.HasPrefabs)
                EditorGUILayout.HelpBox("Prefabs are missing: run Tools ▸ Rotate Rings ▸ Build Gameplay Scene once.", MessageType.Warning);

            DrawFileBar();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawLevelHeader();
            DrawTools();
            EditorGUILayout.Space(6);
            DrawInspector();
            EditorGUILayout.Space(6);
            DrawProblems();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(_tools.ActiveTool == SceneViewLevelTools.SelectTool ? _status + "\n" + _tools.Hint : _tools.Hint, MessageType.None);
        }

        void DrawFileBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("New", EditorStyles.toolbarButton))
                NewLevel();
            if (GUILayout.Button("Open ▾", EditorStyles.toolbarDropDown))
                ShowOpenMenu();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                Save(saveAs: false);
            if (GUILayout.Button("Save As…", EditorStyles.toolbarButton))
                Save(saveAs: true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Frame", EditorStyles.toolbarButton))
                FrameLevel();

            var play = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.9f, 0.45f);
            if (GUILayout.Button("▶ Test Play", EditorStyles.toolbarButton))
                TestPlay();
            GUI.backgroundColor = play;
            EditorGUILayout.EndHorizontal();
        }

        void DrawLevelHeader()
        {
            var file = session.AssetPath != null ? Path.GetFileName(session.AssetPath) : "unsaved";
            EditorGUILayout.LabelField(session.IsDirty ? file + "  •  modified" : file, EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            var name = EditorGUILayout.DelayedTextField("Level name", session.Level.name);
            if (EditorGUI.EndChangeCheck())
                session.Mutate("Rename level", level => level.name = name);

            EditorGUILayout.BeginHorizontal();
            var gridIndex = EditorGUILayout.Popup("Snap", IndexOf(GridSteps, _tools.GridSnap),
                GridSteps.Select(s => s <= 0f ? "Grid off" : $"Grid {s:0.##}").ToArray());
            var angleIndex = EditorGUILayout.Popup(IndexOf(AngleSteps, _tools.AngleSnap),
                AngleSteps.Select(s => s <= 0f ? "Angle off" : $"Angle {s:0}°").ToArray());
            EditorGUILayout.EndHorizontal();

            if (!Mathf.Approximately(GridSteps[gridIndex], _tools.GridSnap))
                EditorPrefs.SetFloat(GridPref, _tools.GridSnap = GridSteps[gridIndex]);
            if (!Mathf.Approximately(AngleSteps[angleIndex], _tools.AngleSnap))
                EditorPrefs.SetFloat(AnglePref, _tools.AngleSnap = AngleSteps[angleIndex]);
        }

        static int IndexOf(float[] steps, float value)
        {
            var index = System.Array.FindIndex(steps, s => Mathf.Approximately(s, value));
            return index < 0 ? 0 : index;
        }

        void DrawTools()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Tools (keys 1–6 in the Scene view)", EditorStyles.boldLabel);

            var tools = _tools.AllTools();
            var labels = tools.Select(ToolLabel).ToArray();
            var current = System.Array.IndexOf(tools, _tools.ActiveTool);
            var picked = GUILayout.SelectionGrid(current, labels, 3, GUILayout.Height(48));
            if (picked != current)
                _tools.SetTool(tools[picked]);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(session.SelectedPiece == null))
            {
                if (GUILayout.Button("Duplicate (Ctrl+D)"))
                    _tools.DuplicateSelection();
            }

            using (new EditorGUI.DisabledScope(session.SelectedPiece == null && session.SelectedLink == null))
            {
                if (GUILayout.Button("Delete (Del)"))
                    _tools.DeleteSelection();
            }
            EditorGUILayout.EndHorizontal();
        }

        string ToolLabel(string tool)
        {
            if (tool.StartsWith("place:"))
                return _shapes.Get(tool.Substring("place:".Length)).DisplayName;
            return tool switch
            {
                SceneViewLevelTools.ClipTool => "Clip",
                SceneViewLevelTools.PinTool => "Pin",
                _ => "Select",
            };
        }

        // ---------- Inspector ----------

        void DrawInspector()
        {
            if (session.SelectedLink != null && session.Level.FindLink(session.SelectedLink) is { } link)
                DrawLinkInspector(link);
            else if (session.SelectedPiece != null && session.Level.FindPiece(session.SelectedPiece) is { } piece)
                DrawPieceInspector(piece);
            else
                DrawLevelInspector();
        }

        void DrawLevelInspector()
        {
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var tube = EditorGUILayout.Slider("Tube radius", session.Level.tubeRadius, 0.03f, 0.3f);
            var clip = EditorGUILayout.Slider("Clip half size", session.Level.clipHalfSize, 0.03f, 0.5f);
            if (EditorGUI.EndChangeCheck())
                session.Mutate("Level settings", level =>
                {
                    level.tubeRadius = tube;
                    level.clipHalfSize = clip;
                });

            EditorGUILayout.LabelField($"{session.Level.pieces.Count} pieces · {session.Level.links.Count} links", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "Work in the Scene view (2D, looking at the XY plane — press Frame).\n" +
                "• Pick a shape tool and click to place it.\n" +
                "• Select: click a tube; drag it or its handles (white = move, yellow = rotate, cyan = reshape).\n" +
                "• Clip: click the owner, then the tube it grips. Pin: click a tube.\n" +
                "• Ctrl+Z / Ctrl+Y undo and redo, Del deletes, F frames the level.",
                MessageType.Info);
        }

        void DrawPieceInspector(PieceDef piece)
        {
            var template = _shapes.TryGet(piece.shape, out var t) ? t : null;
            EditorGUILayout.LabelField(template?.DisplayName ?? piece.shape, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newId = EditorGUILayout.DelayedTextField("Id", piece.id);
            if (EditorGUI.EndChangeCheck())
            {
                var oldId = piece.id;
                var renamed = false;
                session.Mutate("Rename piece", level => renamed = LevelEdits.RenamePiece(level, oldId, newId.Trim()));
                if (renamed)
                    session.SelectPiece(newId.Trim());
                else
                    SetStatus($"Id '{newId}' is empty or already used.");
                GUIUtility.ExitGUI();
            }

            DrawPalette(piece);

            EditorGUI.BeginChangeCheck();
            var motion = (MotionType)EditorGUILayout.EnumPopup("Motion", piece.Motion);
            var position = EditorGUILayout.Vector2Field("Position", piece.position);
            var rotation = EditorGUILayout.FloatField("Rotation (°)", piece.rotation);
            var parameters = piece.parameters;
            if (template != null)
            {
                foreach (var param in template.Params)
                    parameters = parameters.With(param.Field,
                        param.Clamp(EditorGUILayout.FloatField(param.Label, parameters.Get(param.Field))));
            }

            if (EditorGUI.EndChangeCheck())
            {
                var id = piece.id;
                session.Mutate("Edit piece", level =>
                {
                    var target = level.FindPiece(id);
                    target.Motion = motion;
                    LevelEdits.MovePiece(level, id, position);
                    LevelEdits.RotatePiece(level, id, rotation);
                    LevelEdits.SetParameters(level, _shapes, id, parameters);
                });
            }
        }

        void DrawPalette(PieceDef piece)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Color");
            foreach (var color in SampleLevels.Palette)
            {
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = color;
                if (GUILayout.Button(GUIContent.none, GUILayout.Width(18), GUILayout.Height(18)))
                {
                    var id = piece.id;
                    session.Mutate("Change color", level => level.FindPiece(id).Color = color);
                }
                GUI.backgroundColor = previous;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.ColorField(" ", piece.Color);
            if (EditorGUI.EndChangeCheck())
            {
                var id = piece.id;
                session.Mutate("Change color", level => level.FindPiece(id).Color = picked);
            }
        }

        void DrawLinkInspector(LinkDef link)
        {
            EditorGUILayout.LabelField(link.IsPin ? "Pin (fixed to the board)" : "Clip", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newId = EditorGUILayout.DelayedTextField("Id", link.id);
            if (EditorGUI.EndChangeCheck())
            {
                var oldId = link.id;
                var renamed = false;
                session.Mutate("Rename link", level => renamed = LevelEdits.RenameLink(level, oldId, newId.Trim()));
                if (renamed)
                    session.SelectLink(newId.Trim());
                else
                    SetStatus($"Id '{newId}' is empty or already used.");
                GUIUtility.ExitGUI();
            }

            if (!link.IsPin)
                EditorGUILayout.LabelField("Owner", link.ownerId);
            EditorGUILayout.LabelField("Grips", link.targetId);

            EditorGUI.BeginChangeCheck();
            var point = EditorGUILayout.Vector2Field("Grip point", link.point);
            if (EditorGUI.EndChangeCheck())
            {
                var target = session.Level.FindPiece(link.targetId);
                var snapped = target != null && _shapes.TryGet(target.shape, out var template)
                    ? LevelEdits.SnapToPath(target, template, point)
                    : point;
                var id = link.id;
                session.Mutate("Move link", level => level.FindLink(id).point = snapped);
            }
        }

        // ---------- Problems ----------

        void DrawProblems()
        {
            EditorGUILayout.LabelField(_issues.Count == 0 ? "Problems · none" : $"Problems · {_issues.Count}", EditorStyles.boldLabel);
            foreach (var issue in _issues)
            {
                var type = issue.Severity == IssueSeverity.Error ? MessageType.Error : MessageType.Warning;
                EditorGUILayout.HelpBox(issue.Message, type);
                var rect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    if (issue.LinkId != null && session.Level.FindLink(issue.LinkId) != null)
                        session.SelectLink(issue.LinkId);
                    else if (issue.PieceId != null && session.Level.FindPiece(issue.PieceId) != null)
                        session.SelectPiece(issue.PieceId);
                    Event.current.Use();
                }
            }
        }

        // ---------- Files ----------

        bool ConfirmDiscard() =>
            !session.IsDirty || EditorUtility.DisplayDialog("Level Editor", "Discard unsaved changes to this level?", "Discard", "Cancel");

        void NewLevel()
        {
            if (!ConfirmDiscard())
                return;

            session.Load(new LevelDefinition { name = "New Level" }, null);
            SetStatus("New level.");
        }

        void ShowOpenMenu()
        {
            var menu = new GenericMenu();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json"))
                    menu.AddItem(new GUIContent(Path.GetFileNameWithoutExtension(path)), path == session.AssetPath, () => LoadFile(path));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Browse…"), false, () =>
            {
                var full = EditorUtility.OpenFilePanel("Open level", LevelsFolder, "json");
                if (!string.IsNullOrEmpty(full))
                    LoadFile(full);
            });
            menu.ShowAsContext();
        }

        public void LoadFile(string path)
        {
            if (!ConfirmDiscard())
                return;

            try
            {
                var level = LevelJson.Parse(File.ReadAllText(path));
                session.Load(level, ToAssetPath(path));
                FrameLevel();
                SetStatus($"Opened {Path.GetFileName(path)}");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                SetStatus($"Could not open {path}: {e.Message}");
            }
        }

        void Save(bool saveAs)
        {
            var path = session.AssetPath;
            if (saveAs || path == null)
            {
                path = EditorUtility.SaveFilePanelInProject("Save level", LevelEdits.FileNameFor(session.Level.name), "json",
                    "Level JSON files in Assets/_Game/Levels are added to the LevelCatalog.", LevelsFolder);
                if (string.IsNullOrEmpty(path))
                    return;
            }

            File.WriteAllText(Path.GetFullPath(path), LevelJson.ToJson(session.Level));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AddToCatalog(path);
            session.MarkSaved(path);
            SetStatus($"Saved {path}");
        }

        static void AddToCatalog(string path)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(SampleLevelGenerator.CatalogPath);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (catalog == null || asset == null || catalog.levels.Contains(asset) || !path.StartsWith(LevelsFolder))
                return;

            catalog.levels.Add(asset);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static string ToAssetPath(string path)
        {
            var full = Path.GetFullPath(path).Replace('\\', '/');
            var project = Path.GetFullPath(".").Replace('\\', '/') + "/";
            return full.StartsWith(project) ? full.Substring(project.Length) : null;
        }

        // ---------- Scene view / Test Play ----------

        void FrameLevel()
        {
            var sceneView = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            sceneView.in2DMode = true;

            var board = _preview?.Board;
            var bounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 0.1f));
            if (board != null && board.Pieces.Count > 0)
            {
                var first = true;
                foreach (var piece in board.Pieces)
                {
                    foreach (var sample in piece.Path.Sample(0.2f))
                    {
                        var world = (Vector3)piece.LocalToWorld(sample.Point);
                        if (first)
                            bounds = new Bounds(world, Vector3.zero);
                        else
                            bounds.Encapsulate(world);
                        first = false;
                    }
                }
                bounds.Expand(1.5f);
            }

            sceneView.Frame(bounds, false);
            sceneView.Repaint();
        }

        void TestPlay()
        {
            var issues = _issues.Count(i => i.Severity == IssueSeverity.Error);
            if (issues > 0 && !EditorUtility.DisplayDialog("Level Editor",
                    $"The level has {issues} error(s); broken pieces or links will be skipped. Play anyway?", "Play", "Cancel"))
                return;

            TestPlayLauncher.Launch(session.Level);
        }
    }
}
