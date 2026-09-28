using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Native "Save as" / "Open" dialogs.
    ///
    /// • In the editor — EditorUtility file panels.
    /// • In a Windows build — the standard Windows dialog (comdlg32).
    /// • Elsewhere — no dialog: a fixed file in Application.persistentDataPath.
    ///
    /// Returns null when the user cancels.
    /// </summary>
    public static class FileDialogs
    {
        public static string SaveFile(string title, string defaultName, string extension)
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.SaveFilePanel(title, DefaultDirectory, defaultName, extension);
            return string.IsNullOrEmpty(path) ? null : path;
#elif UNITY_STANDALONE_WIN
            return ShowWindowsDialog(title, defaultName + "." + extension, extension, save: true);
#else
            return Path.Combine(Application.persistentDataPath, defaultName + "." + extension);
#endif
        }

        public static string OpenFile(string title, string extension)
        {
#if UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel(title, DefaultDirectory, extension);
            return string.IsNullOrEmpty(path) ? null : path;
#elif UNITY_STANDALONE_WIN
            return ShowWindowsDialog(title, string.Empty, extension, save: false);
#else
            var path = Path.Combine(Application.persistentDataPath, "schema." + extension);
            return File.Exists(path) ? path : null;
#endif
        }

        private static string DefaultDirectory => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int OFN_NOCHANGEDIR     = 0x00000008;
        private const int OFN_PATHMUSTEXIST   = 0x00000800;
        private const int OFN_FILEMUSTEXIST   = 0x00001000;
        private const int OFN_EXPLORER        = 0x00080000;
        private const int MaxPath = 1024;

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetSaveFileNameW(ref OpenFileName ofn);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        private static string ShowWindowsDialog(string title, string defaultName, string extension, bool save)
        {
            var buffer = Marshal.AllocHGlobal(MaxPath * 2);
            try
            {
                // Zeroed buffer, pre-filled with the default file name
                var initial = new char[MaxPath];
                defaultName.CopyTo(0, initial, 0, Math.Min(defaultName.Length, MaxPath - 1));
                Marshal.Copy(initial, 0, buffer, MaxPath);

                var ofn = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf<OpenFileName>(),
                    hwndOwner = GetActiveWindow(),
                    lpstrFilter = $"{Loc.Get("FILE_FILTER_SCHEMA")} (*.{extension})\0*.{extension}\0{Loc.Get("FILE_FILTER_ALL")} (*.*)\0*.*\0\0",
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = MaxPath,
                    lpstrInitialDir = DefaultDirectory,
                    lpstrTitle = title,
                    lpstrDefExt = extension,
                    Flags = OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
                            | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST),
                };

                var ok = save ? GetSaveFileNameW(ref ofn) : GetOpenFileNameW(ref ofn);
                return ok ? Marshal.PtrToStringUni(buffer) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
#endif
    }
}
