using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlacementSystem
{
    /// <summary>
    /// Saving and opening the whole project.
    ///
    ///   F5        — save (the first time asks where; then overwrites that file)
    ///   Shift+F5  — save as…
    ///   F9        — open a saved project (replaces everything in the scene)
    ///
    /// Saved: all placed objects (type, position, rotation, scale), all wires
    /// down to the exact connection point, and the camera view.
    ///
    /// Created automatically on the PlacementSystem object if the scene has none.
    /// </summary>
    public class ProjectSaveManager : MonoBehaviour
    {
        [SerializeField] private PlacementAssetDatabase database;
        [SerializeField] private WireConnectionMode wireConnectionMode;
        [SerializeField] private EditorModeManager modeManager;
        [SerializeField] private SubstationCheckManager checkManager;
        [SerializeField] private FlyingCameraController cameraController;

        /// <summary>File used by F5; null until the project is saved or opened once.</summary>
        public string CurrentPath { get; private set; }

        // ── Setup ─────────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindAnyObjectByType<ProjectSaveManager>() != null || PlacementManager.Instance == null)
                return;

            PlacementManager.Instance.gameObject.AddComponent<ProjectSaveManager>();
        }

        private void Awake()
        {
            if (database == null)
            {
                var ui = FindAnyObjectByType<UIManager>();
                if (ui != null)
                    database = ui.Database;
            }

            if (wireConnectionMode == null) wireConnectionMode = FindAnyObjectByType<WireConnectionMode>();
            if (modeManager == null)        modeManager = FindAnyObjectByType<EditorModeManager>();
            if (checkManager == null)       checkManager = FindAnyObjectByType<SubstationCheckManager>();
            if (cameraController == null)   cameraController = FindAnyObjectByType<FlyingCameraController>();
        }

        private void Update()
        {
            // Not while typing, in a window, or while something is being dragged.
            if (InteractionLock.IsKeyboardCaptured || InteractionLock.IsDraggingAsset || InteractionLock.IsCameraLocked)
                return;

            // F5 — save, Shift+F5 — save as, F9 — open.
            if (InputUtility.WasKeyPressed(Key.F5))
                Save(InputUtility.IsShiftHeld || CurrentPath == null);
            else if (InputUtility.WasKeyPressed(Key.F9))
                Open();
        }

        // ── Commands ──────────────────────────────────────────────────────────

        /// <param name="askForPath">Show the file dialog (first save or "save as").</param>
        public void Save(bool askForPath)
        {
            var path = CurrentPath;
            if (askForPath || path == null)
            {
                path = FileDialogs.SaveFile(Loc.Get("PROJECT_SAVE_DIALOG_TITLE"),
                    CurrentPath != null ? Path.GetFileNameWithoutExtension(CurrentPath) : Loc.Get("PROJECT_DEFAULT_FILE_NAME"),
                    ProjectFile.Extension, "FILE_FILTER_PROJECT");
                if (path == null)
                    return;
            }

            TrySave(path, out var message);
            EditorNotifications.Post(message);
        }

        public void Open()
        {
            var path = FileDialogs.OpenFile(Loc.Get("PROJECT_OPEN_DIALOG_TITLE"), ProjectFile.Extension, "FILE_FILTER_PROJECT");
            if (path == null)
                return;

            TryLoad(path, out var message);
            EditorNotifications.Post(message);
        }

        // ── Save ──────────────────────────────────────────────────────────────

        public bool TrySave(string path, out string message)
        {
            var snapshot = SceneSnapshot.Capture();
            var data = new ProjectData { savedUtc = DateTime.UtcNow.ToString("o") };

            foreach (var placed in snapshot.Objects)
            {
                var t = placed.transform;
                data.objects.Add(new ProjectObject
                {
                    asset = placed.SourceAsset.name,
                    displayName = placed.SourceAsset.DisplayName,
                    position = t.position,
                    rotation = t.rotation,
                    scale = t.localScale,
                });
            }

            foreach (var w in snapshot.Wires)
            {
                data.wires.Add(new SchemaWire
                {
                    objectA = w.ObjectA, connectorA = w.ConnectorA,
                    objectB = w.ObjectB, connectorB = w.ConnectorB,
                });
            }

            var cam = cameraController != null ? cameraController.transform : Camera.main != null ? Camera.main.transform : null;
            if (cam != null)
            {
                data.hasCamera = true;
                data.cameraPosition = cam.position;
                data.cameraRotation = cam.rotation;
            }

            try
            {
                ProjectFile.Write(path, data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                message = Loc.Format("PROJECT_SAVE_FAILED", e.Message);
                return false;
            }

            CurrentPath = path;
            message = Loc.Format("PROJECT_SAVED", Path.GetFileName(path), data.objects.Count, data.wires.Count);
            return true;
        }

        // ── Load ──────────────────────────────────────────────────────────────

        public bool TryLoad(string path, out string message)
        {
            ProjectData data;
            try
            {
                data = ProjectFile.Read(path);
            }
            catch (InvalidDataException e)
            {
                message = e.Message;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                message = Loc.Format("PROJECT_LOAD_FAILED", e.Message);
                return false;
            }

            if (PlacementManager.Instance == null)
            {
                message = Loc.Format("PROJECT_LOAD_FAILED", "PlacementManager");
                return false;
            }

            PrepareSceneForLoading();

            // Equipment by asset name
            var assets = new Dictionary<string, AssetData>(StringComparer.Ordinal);
            if (database != null)
            {
                foreach (var category in database.Categories)
                {
                    if (category == null)
                        continue;
                    foreach (var asset in category.Assets)
                    {
                        if (asset != null)
                            assets[asset.name] = asset;
                    }
                }
            }

            // Objects
            var spawned = new PlacedObject[data.objects.Count];
            var missing = new SortedSet<string>();
            for (var i = 0; i < data.objects.Count; i++)
            {
                var saved = data.objects[i];
                if (saved.asset == null || !assets.TryGetValue(saved.asset, out var asset))
                {
                    missing.Add(string.IsNullOrEmpty(saved.displayName) ? saved.asset : saved.displayName);
                    continue;
                }

                var placed = PlacementManager.Instance.Spawn(asset, saved.position, saved.rotation, saved.scale);
                if (placed == null)
                    continue;

                // Spawn snaps to the grid and lifts onto the ground — restore the exact saved transform.
                placed.transform.SetPositionAndRotation(saved.position, saved.rotation);
                placed.transform.localScale = saved.scale;
                placed.NotifyTransformChanged();
                spawned[i] = placed;
            }

            // Wires
            var wires = 0;
            foreach (var w in data.wires)
            {
                var a = GetConnector(spawned, w.objectA, w.connectorA);
                var b = GetConnector(spawned, w.objectB, w.connectorB);
                if (a != null && b != null && wireConnectionMode != null && wireConnectionMode.Connect(a, b) != null)
                    wires++;
            }

            // Camera
            if (data.hasCamera)
            {
                if (cameraController != null)
                    cameraController.SetPose(data.cameraPosition, data.cameraRotation);
                else if (Camera.main != null)
                    Camera.main.transform.SetPositionAndRotation(data.cameraPosition, data.cameraRotation);
            }

            CurrentPath = path;

            var loaded = 0;
            foreach (var placed in spawned)
            {
                if (placed != null)
                    loaded++;
            }

            message = Loc.Format("PROJECT_LOADED", Path.GetFileName(path), loaded, wires);
            if (missing.Count > 0)
                message += " " + Loc.Format("PROJECT_LOADED_MISSING", string.Join(", ", missing));
            return true;
        }

        /// <summary>Leaves check / wire modes and removes everything placed so far.</summary>
        private void PrepareSceneForLoading()
        {
            if (checkManager != null && checkManager.IsChecking)
                checkManager.EndCheck();

            if (modeManager != null)
                modeManager.SwitchTo(EditorModeManager.EditorMode.Normal);

            SelectionManager.Instance?.Deselect();

            // Wires go together with their objects (see EnergyConnector.OnDestroy).
            foreach (var placed in FindObjectsByType<PlacedObject>())
                PlacementManager.Instance.Remove(placed);
        }

        private static EnergyConnector GetConnector(PlacedObject[] objects, int objectIndex, int connectorIndex)
        {
            if (objectIndex < 0 || objectIndex >= objects.Length || objects[objectIndex] == null)
                return null;

            var connectors = objects[objectIndex].Connectors;
            return connectorIndex >= 0 && connectorIndex < connectors.Count ? connectors[connectorIndex] : null;
        }
    }
}
