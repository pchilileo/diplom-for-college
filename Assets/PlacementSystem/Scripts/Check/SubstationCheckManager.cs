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
    /// loads that file (<see cref="TryReadReference"/> + <see cref="StartCheck"/>): correct wires turn green,
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

        /// <param name="password">Exam password; ignored for <see cref="SchemaMode.Training"/>.</param>
        public bool TrySaveReference(string path, SchemaMode mode, string password, out string message)
        {
            var snapshot = SceneSnapshot.Capture();
            if (snapshot.Wires.Count == 0)
            {
                message = Loc.Get("SAVE_NO_WIRES");
                return false;
            }

            var schema = snapshot.ToSchema();
            schema.id = SubstationSchema.GenerateId();
            schema.mode = mode;
            if (mode == SchemaMode.Exam)
                schema.SetPassword(password);

            try
            {
                SchemaFile.Write(path, schema);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                message = Loc.Format("SAVE_FAILED", e.Message);
                return false;
            }

            message = Loc.Format("SAVE_DONE", schema.DisplayId, schema.ModeName.ToLowerInvariant(),
                Path.GetFileName(path), snapshot.Objects.Count, snapshot.Wires.Count);
            return true;
        }

        // ── Check ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads a reference file. Failures (damaged / modified / foreign files)
        /// are written to the journal as well.
        /// </summary>
        public bool TryReadReference(string path, out SubstationSchema reference, out string message)
        {
            var fileName = Path.GetFileName(path);
            try
            {
                reference = SchemaFile.Read(path);
                message = null;
                return true;
            }
            catch (InvalidDataException e)
            {
                message = e.Message;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                message = Loc.Format("LOAD_FAILED", e.Message);
            }

            reference = null;
            CheckJournal.Append(fileName, null, Loc.Format("JOURNAL_OUTCOME_NOT_OPENED", message));
            return false;
        }

        /// <summary>Records a wrong exam password in the journal.</summary>
        public void LogWrongPassword(SubstationSchema reference, string path)
        {
            CheckJournal.Append(Path.GetFileName(path), reference, Loc.Get("JOURNAL_OUTCOME_WRONG_PASSWORD"));
        }

        /// <summary>
        /// Compares the scene with <paramref name="reference"/> and enters check mode.
        /// For an exam reference the password must have been verified by the caller.
        /// </summary>
        public CheckResult StartCheck(SubstationSchema reference, string path)
        {
            if (IsChecking)
                EndCheck();

            // Leave wire modes and drop an unfinished wire before locking wiring.
            if (modeManager != null)
                modeManager.SwitchTo(EditorModeManager.EditorMode.Normal);

            var result = SchemaMatcher.Compare(reference, SceneSnapshot.Capture());
            result.FileName = Path.GetFileName(path);
            result.SchemaId = reference.DisplayId;
            result.ModeName = reference.ModeName;
            LastResult = result;

            Paint(result.CorrectWires, CorrectColor);
            Paint(result.WrongWires, WrongColor);
            CreateGhosts(result);

            InteractionLock.SetCheckMode(true);
            CheckStarted?.Invoke(result);

            CheckJournal.Append(result.FileName, reference, Loc.Format("JOURNAL_OUTCOME_CHECK",
                result.CorrectWires.Count, result.ReferenceWires, result.WrongWires.Count, result.MissingTotal));

            return result;
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
