using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// All UI texts, read from language files.
    ///
    /// Files: <c>Assets/StreamingAssets/Localization/&lt;code&gt;.lang</c> (ru.lang, en.lang…).
    /// StreamingAssets is copied into the build as a normal folder, so texts can
    /// be edited — and languages added — even in a built trainer, without Unity.
    ///
    /// Format, one text per line:
    /// <code>
    /// # comment
    /// INSPECTOR_TITLE = "Инспектор"
    /// CHECK_RESULT_SCORE = "Проверка: верно {0} из {1}"   ← {0}, {1} are filled in by the program
    /// </code>
    /// Inside quotes: \n = new line, \" = quote, \\ = backslash. The special key
    /// LANGUAGE_NAME is the name shown for the language.
    ///
    /// A key missing in the chosen language falls back to Russian, then to the
    /// key itself (so a forgotten text is easy to spot on screen).
    /// </summary>
    public static class Loc
    {
        public const string DefaultLanguage = "ru";
        private const string PrefKey = "PlacementSystem.Language";
        private const string Extension = ".lang";

        private static Dictionary<string, string> current;
        private static Dictionary<string, string> fallback;
        private static readonly List<(string code, string name)> languages = new();

        /// <summary>Code of the active language, e.g. "ru".</summary>
        public static string Language { get; private set; } = DefaultLanguage;

        /// <summary>Fired after the language has changed — UI texts refresh themselves.</summary>
        public static event Action LanguageChanged;

        public static string Folder => Path.Combine(Application.streamingAssetsPath, "Localization");

        /// <summary>Available languages (code, name), found as *.lang files.</summary>
        public static IReadOnlyList<(string code, string name)> Languages
        {
            get
            {
                EnsureLoaded();
                return languages;
            }
        }

        // Static state survives play mode when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            fallback = null;
            languages.Clear();
            LanguageChanged = null;
        }

        // ── Lookup ────────────────────────────────────────────────────────────

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            EnsureLoaded();
            if (current.TryGetValue(key, out var value) || fallback.TryGetValue(key, out value))
                return value;

            return key;
        }

        /// <summary>True if the key exists in the current language or in Russian.</summary>
        public static bool Has(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            EnsureLoaded();
            return current.ContainsKey(key) || fallback.ContainsKey(key);
        }

        /// <summary><see cref="Get"/> with {0}, {1}… replaced by <paramref name="args"/>.</summary>
        public static string Format(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException)
            {
                // A broken placeholder in a language file must not break the program.
                Debug.LogWarning($"[Loc] Bad placeholders in \"{key}\" ({Language}): {template}");
                return template;
            }
        }

        // ── Language ──────────────────────────────────────────────────────────

        public static void SetLanguage(string code)
        {
            EnsureLoaded();
            ApplyLanguage(code);
            PlayerPrefs.SetString(PrefKey, Language);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
        }

        /// <summary>Switches to the next available language (for a toggle button).</summary>
        public static void NextLanguage()
        {
            var list = Languages;
            if (list.Count < 2)
                return;

            var index = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].code == Language)
                    index = i;
            }
            SetLanguage(list[(index + 1) % list.Count].code);
        }

        /// <summary>Re-reads the language files (e.g. after editing them).</summary>
        public static void Reload()
        {
            current = null;
            fallback = null;
            EnsureLoaded();
            LanguageChanged?.Invoke();
        }

        // ── Loading ───────────────────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (current != null)
                return;

            languages.Clear();
            if (Directory.Exists(Folder))
            {
                foreach (var file in Directory.GetFiles(Folder, "*" + Extension))
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    var texts = LoadFile(code);
                    languages.Add((code, texts.TryGetValue("LANGUAGE_NAME", out var name) ? name : code));
                }
                languages.Sort((a, b) => a.code == DefaultLanguage ? -1 : b.code == DefaultLanguage ? 1 : string.CompareOrdinal(a.code, b.code));
            }
            else
            {
                Debug.LogWarning($"[Loc] Folder with language files not found: {Folder}");
            }

            fallback = LoadFile(DefaultLanguage);
            ApplyLanguage(PlayerPrefs.GetString(PrefKey, DefaultLanguage));
        }

        private static void ApplyLanguage(string code)
        {
            var exists = languages.Exists(l => l.code == code);
            Language = exists ? code : DefaultLanguage;
            current = Language == DefaultLanguage ? fallback : LoadFile(Language);
        }

        private static Dictionary<string, string> LoadFile(string code)
        {
            var path = Path.Combine(Folder, code + Extension);
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8), path) : new Dictionary<string, string>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new Dictionary<string, string>();
            }
        }

        /// <summary>Parses <c>KEY = "value"</c> lines. Bad lines are reported and skipped.</summary>
        public static Dictionary<string, string> Parse(string text, string sourceName)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var lines = text.Split('\n');

            for (var n = 0; n < lines.Length; n++)
            {
                var line = lines[n].Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    Debug.LogWarning($"[Loc] {sourceName}:{n + 1}: expected KEY = \"text\"");
                    continue;
                }

                var key = line.Substring(0, eq).Trim();
                var raw = line.Substring(eq + 1).Trim();

                string value;
                if (raw.Length >= 2 && raw[0] == '"')
                {
                    var close = FindClosingQuote(raw);
                    if (close < 0)
                    {
                        Debug.LogWarning($"[Loc] {sourceName}:{n + 1}: missing closing quote for {key}");
                        continue;
                    }
                    value = Unescape(raw.Substring(1, close - 1));
                }
                else
                {
                    value = Unescape(raw);
                }

                if (result.ContainsKey(key))
                    Debug.LogWarning($"[Loc] {sourceName}:{n + 1}: {key} is defined twice, the last one wins");
                result[key] = value;
            }

            return result;
        }

        private static int FindClosingQuote(string raw)
        {
            for (var i = 1; i < raw.Length; i++)
            {
                if (raw[i] == '\\')
                    i++;   // skip escaped char
                else if (raw[i] == '"')
                    return i;
            }
            return -1;
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0)
                return s;

            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++)
            {
                if (s[i] != '\\' || i == s.Length - 1)
                {
                    sb.Append(s[i]);
                    continue;
                }

                var next = s[++i];
                sb.Append(next switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _   => next,   // \" \\ and anything else
                });
            }
            return sb.ToString();
        }
    }
}
