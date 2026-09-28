using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Wiring check against a reference ("эталон").
    ///
    /// Person A builds a substation and saves its wiring to a *.substation file
    /// (<see cref="TrySaveReference"/>). Person B builds their own version and
    /// loads that file (<see cref="TryStartCheck"/>): correct wires turn green,
    /// wrong / extra ones red, and missing ones appear as pulsing orange ghosts.
    /// While checking, wires can't be edited and objects can't be placed or
    /// deleted — only moved, rotated and scaled. <see cref="EndCheck"/> restores
    /// the normal editor.
    /// </summary>
    public class SubstationCheckManager : MonoBehaviour
    {
        public static readonly Color CorrectColor = new(0.2f, 0.85f, 0.3f);
        public static readonly Color WrongColor   = new(0.95f, 0.2f, 0.2f);
        public static readonly Color MissingColor = new(1f, 0.55f, 0.05f);

        [SerializeField] private EditorModeManager modeManager;

        private readonly List<WireConnection> coloredWires = new();
        private readonly List<GhostWire> ghosts = new();
        private Transform ghostRoot;

        public bool IsChecking => InteractionLock.IsCheckMode;
        public CheckResult LastResult { get; private set; }

        public event Action<CheckResult> CheckStarted;
        public event Action CheckEnded;

        private void Awake()
        {
            if (modeManager == null)
                modeManager = FindAnyObjectByType<EditorModeManager>();
        }

        private void OnDestroy()
        {
            if (IsChecking)
                EndCheck();
        }

        // ── Save ──────────────────────────────────────────────────────────────

        public bool TrySaveReference(string path, out string message)
        {
            var snapshot = SceneSnapshot.Capture();
            if (snapshot.Wires.Count == 0)
            {
                message = "На сцене нет ни одного провода — сохранять нечего";
                return false;
            }

            try
            {
                SchemaFile.Write(path, snapshot.ToSchema());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                message = "Не удалось сохранить файл: " + e.Message;
                return false;
            }

            message = $"Эталон сохранён: {Path.GetFileName(path)} (объектов: {snapshot.Objects.Count}, проводов: {snapshot.Wires.Count})";
            return true;
        }

        // ── Check ─────────────────────────────────────────────────────────────

        public bool TryStartCheck(string path, out string message)
        {
            SubstationSchema reference;
            try
            {
                reference = SchemaFile.Read(path);
            }
            catch (InvalidDataException e)
            {
                message = e.Message;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                message = "Не удалось открыть файл: " + e.Message;
                return false;
            }

            if (IsChecking)
                EndCheck();

            // Leave wire modes and drop an unfinished wire before locking wiring.
            modeManager?.SwitchTo(EditorModeManager.EditorMode.Normal);

            var result = SchemaMatcher.Compare(reference, SceneSnapshot.Capture());
            result.FileName = Path.GetFileName(path);
            LastResult = result;

            Paint(result.CorrectWires, CorrectColor);
            Paint(result.WrongWires, WrongColor);
            CreateGhosts(result);

            InteractionLock.SetCheckMode(true);
            CheckStarted?.Invoke(result);

            message = result.IsPerfect
                ? "Проверка: всё подключено верно"
                : $"Проверка: верно {result.CorrectWires.Count} из {result.ReferenceWires}";
            return true;
        }

        public void EndCheck()
        {
            foreach (var wire in coloredWires)
            {
                if (wire != null)
                    wire.RestoreDefaultColor();
            }
            coloredWires.Clear();

            foreach (var ghost in ghosts)
            {
                if (ghost != null)
                    Destroy(ghost.gameObject);
            }
            ghosts.Clear();

            LastResult = null;
            InteractionLock.SetCheckMode(false);
            CheckEnded?.Invoke();
        }

        private void Paint(List<WireConnection> wires, Color color)
        {
            foreach (var wire in wires)
            {
                if (wire == null)
                    continue;
                wire.SetColor(color);
                coloredWires.Add(wire);
            }
        }

        private void CreateGhosts(CheckResult result)
        {
            if (ghostRoot == null)
            {
                ghostRoot = new GameObject("CheckGhostWires").transform;
                ghostRoot.SetParent(transform, false);
            }

            foreach (var (a, b) in result.MissingWires)
            {
                if (a != null && b != null)
                    ghosts.Add(GhostWire.Create(a, b, MissingColor, ghostRoot));
            }
        }
    }
}
