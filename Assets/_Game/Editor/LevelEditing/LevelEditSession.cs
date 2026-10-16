using System;
using RotateRings.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace RotateRings.EditorTools.LevelEditing
{
    /// <summary>
    /// The level being edited in Edit Mode, held as JSON on a ScriptableObject so Unity's own Undo/Redo
    /// (Ctrl+Z / Ctrl+Y, Edit menu) records every change and the work survives script reloads.
    /// </summary>
    public class LevelEditSession : ScriptableObject
    {
        [SerializeField] string json;
        [SerializeField] string assetPath;
        [SerializeField] bool dirty;
        [SerializeField] string selectedPiece;
        [SerializeField] string selectedLink;

        LevelDefinition _level;

        public event Action Changed;

        public static LevelEditSession Create(LevelDefinition level)
        {
            var session = CreateInstance<LevelEditSession>();
            session.hideFlags = HideFlags.DontSave;
            session.Load(level, null);
            return session;
        }

        public LevelDefinition Level => _level ??= LevelJson.Parse(string.IsNullOrEmpty(json) ? "{}" : json);

        /// <summary>Project path of the JSON file (Assets/...), or null if never saved.</summary>
        public string AssetPath => string.IsNullOrEmpty(assetPath) ? null : assetPath;

        public bool IsDirty => dirty;
        public string SelectedPiece => string.IsNullOrEmpty(selectedPiece) ? null : selectedPiece;
        public string SelectedLink => string.IsNullOrEmpty(selectedLink) ? null : selectedLink;

        public void Load(LevelDefinition level, string path)
        {
            _level = level;
            json = LevelJson.ToJson(level);
            assetPath = path;
            dirty = false;
            selectedPiece = selectedLink = null;
            Undo.ClearUndo(this);
            Changed?.Invoke();
        }

        /// <summary>
        /// Records an undo step, applies the change and notifies listeners. Each call is its own undo step,
        /// except with continueStep, which merges into the previous one (the rest of a drag).
        /// </summary>
        public void Mutate(string label, Action<LevelDefinition> change, bool continueStep = false)
        {
            if (!continueStep)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(label);
            }

            Undo.RecordObject(this, label);
            change(Level);
            json = LevelJson.ToJson(Level);
            dirty = true;
            Changed?.Invoke();
        }

        public void SelectPiece(string id) => SetSelection(id, null);
        public void SelectLink(string id) => SetSelection(null, id);
        public void ClearSelection() => SetSelection(null, null);

        void SetSelection(string piece, string link)
        {
            if (selectedPiece == piece && selectedLink == link)
                return;

            selectedPiece = piece;
            selectedLink = link;
            Changed?.Invoke();
        }

        public void MarkSaved(string path)
        {
            assetPath = path;
            dirty = false;
            Changed?.Invoke();
        }

        /// <summary>After Undo/Redo restored the serialized JSON.</summary>
        public void ReloadFromJson()
        {
            _level = null;
            if (SelectedPiece != null && Level.FindPiece(SelectedPiece) == null)
                selectedPiece = null;
            if (SelectedLink != null && Level.FindLink(SelectedLink) == null)
                selectedLink = null;
            Changed?.Invoke();
        }
    }
}
