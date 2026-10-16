using System;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using UnityEditor;
using UnityEngine;

namespace RotateRings.EditorTools.LevelEditing
{
    /// <summary>
    /// Mouse and keyboard editing in the Scene view: select, drag, rotate and reshape pieces through handles,
    /// place new shapes, add clips and pins. Everything goes through LevelEditSession, so Unity Undo applies.
    /// </summary>
    public class SceneViewLevelTools
    {
        public const string SelectTool = "select";
        public const string ClipTool = "clip";
        public const string PinTool = "pin";
        public static string PlaceTool(string templateId) => "place:" + templateId;

        const float PieceHitTolerance = 0.22f;
        const float LinkHitRadius = 0.16f;
        const float RotateHandleGap = 0.35f;

        static readonly Color PivotColor = Color.white;
        static readonly Color RotateColor = new(1f, 0.82f, 0.1f);
        static readonly Color ShapeColor = new(0.1f, 0.85f, 0.95f);
        static readonly Color LinkColor = new(0.95f, 0.25f, 0.85f);
        static readonly Color OutlineColor = new(1f, 1f, 1f, 0.9f);

        readonly Func<LevelEditSession> _session;
        readonly Func<SceneViewLevelPreview> _preview;
        readonly ShapeRegistry _shapes;
        readonly Action<string> _status;
        readonly Action _frame;

        string _clipOwner;
        bool _draggingBody;
        bool _dragStepOpen;
        Vector2 _dragStartPointer;
        Vector2 _dragStartPosition;

        public string ActiveTool { get; private set; } = SelectTool;
        public float GridSnap { get; set; } = 0.05f;
        public float AngleSnap { get; set; } = 5f;

        public event Action ToolChanged;

        public SceneViewLevelTools(Func<LevelEditSession> session, Func<SceneViewLevelPreview> preview, ShapeRegistry shapes,
            Action<string> status, Action frame)
        {
            _session = session;
            _preview = preview;
            _shapes = shapes;
            _status = status;
            _frame = frame;
        }

        LevelEditSession Session => _session();
        BoardModel Board => _preview()?.Board;
        LevelDefinition Level => Session.Level;

        public string Hint => ActiveTool switch
        {
            SelectTool => "Click a tube to select · drag to move · yellow = rotate · cyan = reshape · pink = slide a link",
            ClipTool => _clipOwner == null ? "Clip: click the piece that owns the clip" : $"Clip from '{_clipOwner}': click the tube it grips",
            PinTool => "Pin: click a tube to fix a silver sleeve on the board",
            _ => "Click in the Scene view to place the shape",
        };

        public void SetTool(string tool)
        {
            ActiveTool = tool;
            _clipOwner = null;
            ToolChanged?.Invoke();
        }

        // ---------- Scene GUI ----------

        public void OnSceneGUI(SceneView sceneView)
        {
            var session = Session;
            if (session == null || Board == null)
                return;

            var e = Event.current;
            if (e.rawType == EventType.MouseUp)
                _dragStepOpen = false;

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(controlId);

            DrawLabelsAndOutline();
            DrawHandles();
            HandleKeyboard(e);
            HandleMouse(e, controlId);
        }

        void DrawLabelsAndOutline()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var labelStyle = EditorStyles.miniBoldLabel;
            foreach (var piece in Board.Pieces)
            {
                var selected = piece.Id == Session.SelectedPiece;
                if (selected || piece.Id == _clipOwner)
                {
                    Handles.color = OutlineColor;
                    var samples = piece.Path.Sample(0.05f);
                    var points = new Vector3[samples.Count];
                    for (var i = 0; i < samples.Count; i++)
                        points[i] = piece.LocalToWorld(samples[i].Point);
                    Handles.DrawAAPolyLine(3f, points);
                }

                Handles.Label(piece.Position + new Vector2(0.08f, -0.08f), piece.Id, labelStyle);
            }
        }

        void DrawHandles()
        {
            var selectedLink = Session.SelectedLink != null ? Level.FindLink(Session.SelectedLink) : null;
            if (selectedLink != null)
            {
                DrawLinkHandle(selectedLink);
                return;
            }

            var def = Session.SelectedPiece != null ? Level.FindPiece(Session.SelectedPiece) : null;
            var piece = def != null ? Board.FindPiece(def.id) : null;
            if (piece == null)
                return;

            // Pivot: move the piece.
            if (Handle(def.position, PivotColor, 0.8f, out var pivot))
            {
                var target = Snap(pivot);
                if (target != def.position)
                    DragMutate("Move piece", level => LevelEdits.MovePiece(level, def.id, target));
                return;
            }

            // Rotate.
            var extent = 0.3f;
            foreach (var sample in piece.Path.Sample(0.1f))
                extent = Mathf.Max(extent, sample.Point.magnitude);
            var rotatePosition = def.position + AngleMath.Direction(def.rotation) * (extent + RotateHandleGap);
            if (Handle(rotatePosition, RotateColor, 1f, out var rotated))
            {
                var angle = SnapAngle(AngleMath.AngleOf(rotated - def.position));
                var newRotation = def.rotation + Mathf.DeltaAngle(def.rotation, angle);
                if (!Mathf.Approximately(newRotation, def.rotation))
                    DragMutate("Rotate piece", level => LevelEdits.RotatePiece(level, def.id, newRotation));
                return;
            }

            // Shape handles from the template.
            foreach (var handle in piece.Template.Handles)
            {
                var world = piece.LocalToWorld(handle.Position(def.parameters));
                if (!Handle(world, ShapeColor, 1f, out var moved))
                    continue;

                var local = PieceModel.Rotate(moved - def.position, -def.rotation);
                var parameters = handle.Apply(def.parameters, Snap(local));
                if (!parameters.Equals(def.parameters))
                    DragMutate("Reshape piece", level => LevelEdits.SetParameters(level, _shapes, def.id, parameters));
                return;
            }
        }

        void DrawLinkHandle(LinkDef link)
        {
            var target = Level.FindPiece(link.targetId);
            if (target == null || !_shapes.TryGet(target.shape, out var template))
                return;

            if (Handle(link.point, LinkColor, 1f, out var moved))
            {
                var point = LevelEdits.SnapToPath(target, template, moved);
                if (point != link.point)
                    DragMutate("Move link", _ => link.point = point);
            }
        }

        static bool Handle(Vector2 position, Color color, float scale, out Vector2 result)
        {
            var world = new Vector3(position.x, position.y, -0.3f);
            var size = HandleUtility.GetHandleSize(world) * 0.08f * scale;
            Handles.color = color;
            EditorGUI.BeginChangeCheck();
            var moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
            result = moved;
            return EditorGUI.EndChangeCheck();
        }

        void HandleKeyboard(Event e)
        {
            if (e.type != EventType.KeyDown)
                return;

            switch (e.keyCode)
            {
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    DeleteSelection();
                    e.Use();
                    break;
                case KeyCode.Escape:
                    SetTool(SelectTool);
                    e.Use();
                    break;
                case KeyCode.F:
                    _frame();
                    e.Use();
                    break;
                case KeyCode.D when e.control || e.command:
                    DuplicateSelection();
                    e.Use();
                    break;
                case >= KeyCode.Alpha1 and <= KeyCode.Alpha9 when !e.control && !e.alt:
                {
                    var tools = AllTools();
                    var index = e.keyCode - KeyCode.Alpha1;
                    if (index < tools.Length)
                    {
                        SetTool(tools[index]);
                        e.Use();
                    }
                    break;
                }
            }
        }

        public string[] AllTools()
        {
            var tools = new string[_shapes.All.Count + 3];
            tools[0] = SelectTool;
            for (var i = 0; i < _shapes.All.Count; i++)
                tools[i + 1] = PlaceTool(_shapes.All[i].Id);
            tools[^2] = ClipTool;
            tools[^1] = PinTool;
            return tools;
        }

        void HandleMouse(Event e, int controlId)
        {
            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when e.button == 0 && !e.alt && HandleUtility.nearestControl == controlId:
                    PointerDown(ToBoard(e.mousePosition));
                    GUIUtility.hotControl = controlId;
                    e.Use();
                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    if (_draggingBody)
                        DragBody(ToBoard(e.mousePosition));
                    e.Use();
                    break;

                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    _draggingBody = false;
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
            }
        }

        static Vector2 ToBoard(Vector2 guiPoint)
        {
            var ray = HandleUtility.GUIPointToWorldRay(guiPoint);
            if (Mathf.Abs(ray.direction.z) < 1e-5f)
                return ray.origin;

            var t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }

        // ---------- Tools ----------

        public void PointerDown(Vector2 world)
        {
            _draggingBody = false;
            _dragStepOpen = false;

            if (ActiveTool.StartsWith("place:"))
            {
                Place(ActiveTool.Substring("place:".Length), world);
                return;
            }

            switch (ActiveTool)
            {
                case ClipTool:
                case PinTool:
                    LinkClick(world);
                    return;
            }

            var link = HitLink(world);
            if (link != null)
            {
                Session.SelectLink(link.Id);
                return;
            }

            var piece = Board.FindPieceAt(world, PieceHitTolerance);
            if (piece == null)
            {
                Session.ClearSelection();
                return;
            }

            Session.SelectPiece(piece.Id);
            _draggingBody = true;
            _dragStartPointer = world;
            _dragStartPosition = Level.FindPiece(piece.Id).position;
        }

        public void DragBody(Vector2 world)
        {
            var id = Session.SelectedPiece;
            if (id == null)
                return;

            var target = Snap(_dragStartPosition + (world - _dragStartPointer));
            if (Level.FindPiece(id).position != target)
                DragMutate("Move piece", level => LevelEdits.MovePiece(level, id, target));
        }

        void Place(string templateId, Vector2 world)
        {
            var template = _shapes.Get(templateId);
            PieceDef piece = null;
            Session.Mutate($"Add {template.DisplayName}", level => piece = LevelEdits.AddPiece(level, template, Snap(world)));
            Session.SelectPiece(piece.id);
            SetTool(SelectTool);
        }

        void LinkClick(Vector2 world)
        {
            var piece = Board.FindPieceAt(world, PieceHitTolerance);
            if (piece == null)
            {
                _status("Nothing there — click on a tube.");
                return;
            }

            var pin = ActiveTool == PinTool;
            if (!pin && _clipOwner == null)
            {
                _clipOwner = piece.Id;
                Session.SelectPiece(piece.Id);
                _status(Hint);
                return;
            }

            if (!pin && piece.Id == _clipOwner)
            {
                _status("A clip must grip another piece.");
                return;
            }

            var target = Level.FindPiece(piece.Id);
            var point = LevelEdits.SnapToPath(target, piece.Template, world);
            var kind = pin ? LinkDef.PinKind : LinkDef.ClipKind;
            var owner = _clipOwner;
            LinkDef link = null;
            Session.Mutate(pin ? "Add pin" : "Add clip", level => link = LevelEdits.AddLink(level, kind, owner, piece.Id, point));
            Session.SelectLink(link.id);
            _clipOwner = null;
            _status(pin ? $"Pinned '{piece.Id}'." : $"Added {link.id}.");
        }

        LinkModel HitLink(Vector2 world)
        {
            LinkModel best = null;
            var bestDistance = LinkHitRadius;
            foreach (var link in Board.Links)
            {
                var distance = (link.GripPoint - world).magnitude;
                if (distance <= bestDistance)
                {
                    best = link;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public void DeleteSelection()
        {
            if (Session.SelectedLink != null)
            {
                var id = Session.SelectedLink;
                Session.Mutate("Delete link", level => LevelEdits.DeleteLink(level, id));
            }
            else if (Session.SelectedPiece != null)
            {
                var id = Session.SelectedPiece;
                Session.Mutate("Delete piece", level => LevelEdits.DeletePiece(level, id));
            }

            Session.ClearSelection();
        }

        public void DuplicateSelection()
        {
            var id = Session.SelectedPiece;
            if (id == null)
                return;

            PieceDef copy = null;
            Session.Mutate("Duplicate piece", level => copy = LevelEdits.Duplicate(level, id, new Vector2(0.5f, -0.5f)));
            if (copy != null)
                Session.SelectPiece(copy.id);
        }

        /// <summary>A whole drag (until the mouse is released) becomes a single undo step.</summary>
        void DragMutate(string label, Action<LevelDefinition> change)
        {
            Session.Mutate(label, change, continueStep: _dragStepOpen);
            _dragStepOpen = true;
        }

        /// <summary>Ends the current drag's undo step (mouse released).</summary>
        public void EndDrag() => _dragStepOpen = false;

        Vector2 Snap(Vector2 point) =>
            GridSnap <= 0f ? point : new Vector2(Mathf.Round(point.x / GridSnap) * GridSnap, Mathf.Round(point.y / GridSnap) * GridSnap);

        float SnapAngle(float degrees) => AngleSnap <= 0f ? degrees : Mathf.Round(degrees / AngleSnap) * AngleSnap;
    }
}
