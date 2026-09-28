using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PlacementSystem
{
    [Serializable]
    public class JournalEntry
    {
        /// <summary>Local time, "dd.MM.yyyy HH:mm:ss".</summary>
        public string time;
        public string fileName;
        public string schemaId;
        public string mode;
        public string outcome;
    }

    /// <summary>
    /// Log of every attempt to load a reference: when, which file (name and
    /// the ID stored inside it), which mode and what came out of it. There is
    /// deliberately no way to clear it from the program.
    ///
    /// The log is an encrypted file in Application.persistentDataPath. The number
    /// of entries and a fingerprint of the file are also kept in PlayerPrefs,
    /// so deleting the log, editing it or putting back an older copy is detected
    /// and reported as a permanent warning when the journal is viewed.
    /// </summary>
    public static class CheckJournal
    {
        private static readonly byte[] Magic = { (byte)'P', (byte)'J', (byte)'R', (byte)'N' };

        private const string CountKey       = "PlacementSystem.CheckJournal.Count";
        private const string FingerprintKey = "PlacementSystem.CheckJournal.Fingerprint";
        private const string TamperKey      = "PlacementSystem.CheckJournal.Tamper";

        [Serializable]
        private class JournalData
        {
            public List<JournalEntry> entries = new();
        }

        // The editor and a build share persistentDataPath but not PlayerPrefs:
        // separate files keep them from flagging each other as tampering.
        private static string FilePath => Path.Combine(Application.persistentDataPath,
            Application.isEditor ? "check_journal_editor.dat" : "check_journal.dat");

        // ── Public API ────────────────────────────────────────────────────────

        public static void Append(string fileName, SubstationSchema schema, string outcome)
        {
            var data = LoadData();
            data.entries.Add(new JournalEntry
            {
                time = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"),
                fileName = fileName,
                schemaId = schema != null ? schema.DisplayId : "—",
                mode = schema != null ? schema.ModeName : "—",
                outcome = outcome,
            });

            try
            {
                var bytes = SecureContainer.Protect(Magic, Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
                File.WriteAllBytes(FilePath, bytes);

                PlayerPrefs.SetInt(CountKey, data.entries.Count);
                PlayerPrefs.SetString(FingerprintKey, SecureContainer.Fingerprint(bytes));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>All entries, oldest first.</summary>
        public static IReadOnlyList<JournalEntry> Load()
        {
            return LoadData().entries;
        }

        /// <summary>Non-empty if the journal was ever found deleted, modified or replaced.</summary>
        public static string TamperWarning => PlayerPrefs.GetString(TamperKey, string.Empty);

        // ── Internals ─────────────────────────────────────────────────────────

        private static JournalData LoadData()
        {
            var expectedCount = PlayerPrefs.GetInt(CountKey, 0);

            if (!File.Exists(FilePath))
            {
                if (expectedCount > 0)
                {
                    ReportTampering("файл журнала был удалён");
                    ResetExpected();
                }
                return new JournalData();
            }

            byte[] bytes;
            JournalData data;
            try
            {
                bytes = File.ReadAllBytes(FilePath);
                var plain = SecureContainer.Unprotect(Magic, bytes, "журналом проверок");
                data = JsonUtility.FromJson<JournalData>(Encoding.UTF8.GetString(plain)) ?? new JournalData();
                data.entries ??= new List<JournalEntry>();
            }
            catch (Exception)
            {
                // Keep the broken file as evidence and start a new journal.
                ReportTampering("файл журнала был изменён или повреждён");
                TryBackUpBrokenFile();
                ResetExpected();
                return new JournalData();
            }

            var expectedFingerprint = PlayerPrefs.GetString(FingerprintKey, string.Empty);
            if (data.entries.Count != expectedCount ||
                (expectedFingerprint.Length > 0 && SecureContainer.Fingerprint(bytes) != expectedFingerprint))
            {
                ReportTampering("файл журнала был заменён другой копией");
                // Accept this copy from now on, but the warning stays.
                PlayerPrefs.SetInt(CountKey, data.entries.Count);
                PlayerPrefs.SetString(FingerprintKey, SecureContainer.Fingerprint(bytes));
                PlayerPrefs.Save();
            }

            return data;
        }

        private static void ReportTampering(string reason)
        {
            var line = $"{DateTime.Now:dd.MM.yyyy HH:mm} — {reason}";
            var previous = TamperWarning;
            PlayerPrefs.SetString(TamperKey, string.IsNullOrEmpty(previous) ? line : previous + "\n" + line);
            PlayerPrefs.Save();
        }

        /// <summary>Start over with an empty journal (the tamper warning is kept).</summary>
        private static void ResetExpected()
        {
            PlayerPrefs.SetInt(CountKey, 0);
            PlayerPrefs.SetString(FingerprintKey, string.Empty);
            PlayerPrefs.Save();
        }

        private static void TryBackUpBrokenFile()
        {
            try
            {
                var backup = FilePath + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Move(FilePath, backup);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
