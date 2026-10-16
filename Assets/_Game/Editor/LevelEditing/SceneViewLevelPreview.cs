using System;
using System.Collections.Generic;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Model;
using RotateRings.Gameplay.Shapes;
using RotateRings.Gameplay.View;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RotateRings.EditorTools.LevelEditing
{
    /// <summary>
    /// Shows the edited level in the Scene view with the game's own PieceView / LinkView prefabs, under a hidden
    /// root that is never saved, so the open scene is not modified.
    /// </summary>
    public class SceneViewLevelPreview : IDisposable
    {
        const string PrefabsFolder = "Assets/_Game/Prefabs";

        readonly ShapeRegistry _shapes;
        readonly BoardModelFactory _factory;
        readonly PathMeshCache _meshes = new();
        readonly PieceView _piecePrefab;
        readonly LinkView _linkPrefab;
        readonly Dictionary<string, PieceView> _views = new();

        GameObject _root;

        public BoardModel Board { get; private set; }

        public SceneViewLevelPreview(ShapeRegistry shapes)
        {
            _shapes = shapes;
            _factory = new BoardModelFactory(shapes);
            _piecePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/PieceView.prefab")?.GetComponent<PieceView>();
            _linkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/LinkView.prefab")?.GetComponent<LinkView>();
        }

        public bool HasPrefabs => _piecePrefab != null && _linkPrefab != null;

        public void Rebuild(LevelDefinition level, string selectedPiece)
        {
            Clear();
            Board = _factory.Create(LevelEdits.Sanitized(level, _shapes));
            if (!HasPrefabs)
                return;

            _root = new GameObject("RotateRings Level Preview") { hideFlags = HideFlags.HideAndDontSave };
            AddLightIfSceneHasNone();

            foreach (var piece in Board.Pieces)
            {
                var view = Object.Instantiate(_piecePrefab, _root.transform);
                view.Setup(piece, _meshes.Get(piece));
                view.transform.SetParent(_root.transform, true);
                if (piece.Id == selectedPiece)
                    view.SetColor(Color.Lerp(piece.Color, Color.white, 0.45f));
                _views[piece.Id] = view;
            }

            foreach (var link in Board.Links)
            {
                var view = Object.Instantiate(_linkPrefab, _root.transform);
                view.Setup(link, link.Owner != null ? _views[link.Owner.Id] : null);
                if (link.IsPin)
                    view.transform.SetParent(_root.transform, true);
            }

            HideRecursively(_root.transform);
        }

        void AddLightIfSceneHasNone()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.isActiveAndEnabled)
                    return;
            }

            var lightGo = new GameObject("Preview Light");
            lightGo.transform.SetParent(_root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(35f, 25f, 0f);
            var previewLight = lightGo.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 1.15f;
            previewLight.color = new Color(1f, 0.97f, 0.92f);
        }

        static void HideRecursively(Transform transform)
        {
            transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (Transform child in transform)
                HideRecursively(child);
        }

        public void Clear()
        {
            _views.Clear();
            if (_root != null)
                Object.DestroyImmediate(_root);
            _root = null;

            Board?.Dispose();
            Board = null;
        }

        public void Dispose()
        {
            Clear();
            _meshes.Dispose();
        }
    }
}
