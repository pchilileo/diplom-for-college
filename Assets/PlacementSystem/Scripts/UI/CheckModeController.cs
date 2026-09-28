using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    /// <summary>
    /// UI of the wiring check.
    ///
    /// F2 opens a window with pages:
    /// • main     — save a reference / load and check / journal / leave the check;
    /// • save     — choose Training or Exam (exam needs a password), then pick the file;
    /// • password — asked when an exam reference is loaded;
    /// • journal  — every load attempt: time, file, reference ID, mode, result.
    ///
    /// While checking, a banner at the top shows the result and an exit button.
    /// </summary>
    public class CheckModeController : MonoBehaviour
    {
        private const int MinPasswordLength = 4;

        [SerializeField] private SubstationCheckManager checkManager;

        [Header("Window")]
        [SerializeField] private GameObject menu;
        [Tooltip("Dark background behind the window: a click outside the window closes it.")]
        [SerializeField] private Button dimButton;

        [Header("Main page")]
        [SerializeField] private GameObject mainPage;
        [SerializeField] private TMP_Text menuStatus;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button journalButton;
        [SerializeField] private Button exitCheckButton;
        [SerializeField] private Button closeButton;

        [Header("Save page")]
        [SerializeField] private GameObject savePage;
        [SerializeField] private Button trainingModeButton;
        [SerializeField] private Button examModeButton;
        [SerializeField] private GameObject savePasswordGroup;
        [SerializeField] private TMP_InputField savePasswordField;
        [SerializeField] private TMP_Text saveError;
        [SerializeField] private Button saveConfirmButton;
        [SerializeField] private Button saveBackButton;

        [Header("Password page")]
        [SerializeField] private GameObject passwordPage;
        [SerializeField] private TMP_Text passwordInfo;
        [SerializeField] private TMP_InputField loadPasswordField;
        [SerializeField] private TMP_Text passwordError;
        [SerializeField] private Button passwordConfirmButton;
        [SerializeField] private Button passwordCancelButton;

        [Header("Journal page")]
        [SerializeField] private GameObject journalPage;
        [SerializeField] private TMP_Text journalWarning;
        [SerializeField] private TMP_Text journalText;
        [SerializeField] private Button journalBackButton;

        [Header("Check banner")]
        [SerializeField] private GameObject banner;
        [SerializeField] private TMP_Text bannerTitle;
        [SerializeField] private TMP_Text bannerStats;
        [SerializeField] private TMP_Text bannerDetails;
        [SerializeField] private Button bannerExitButton;

        private SchemaMode saveMode = SchemaMode.Training;

        // Exam reference waiting for the password
        private SubstationSchema pendingReference;
        private string pendingPath;

        private bool IsMenuOpen => menu != null && menu.activeSelf;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (checkManager == null)
                checkManager = FindAnyObjectByType<SubstationCheckManager>();
            if (checkManager == null)
                checkManager = gameObject.AddComponent<SubstationCheckManager>();

            // Explicit null checks: "?." does not see Unity's fake-null objects.
            Listen(dimButton, CloseMenu);

            Listen(saveButton, ShowSavePage);
            Listen(loadButton, OnLoad);
            Listen(journalButton, ShowJournalPage);
            Listen(exitCheckButton, OnExitCheck);
            Listen(closeButton, CloseMenu);

            Listen(trainingModeButton, () => SetSaveMode(SchemaMode.Training));
            Listen(examModeButton, () => SetSaveMode(SchemaMode.Exam));
            Listen(saveConfirmButton, OnSaveConfirm);
            Listen(saveBackButton, ShowMainPage);

            Listen(passwordConfirmButton, OnPasswordConfirm);
            Listen(passwordCancelButton, CloseMenu);

            Listen(journalBackButton, ShowMainPage);
            Listen(bannerExitButton, OnExitCheck);

            // Enter in a password field = confirm
            if (savePasswordField != null)
                savePasswordField.onSubmit.AddListener(_ => OnSaveConfirm());
            if (loadPasswordField != null)
                loadPasswordField.onSubmit.AddListener(_ => OnPasswordConfirm());

            if (menu != null)
                menu.SetActive(false);
            if (banner != null)
                banner.SetActive(false);
        }

        private void OnEnable()
        {
            checkManager.CheckStarted += ShowBanner;
            checkManager.CheckEnded += HideBanner;
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            checkManager.CheckStarted -= ShowBanner;
            checkManager.CheckEnded -= HideBanner;
            Loc.LanguageChanged -= OnLanguageChanged;
            if (IsMenuOpen)
                CloseMenu();
        }

        private void Update()
        {
            if (WasMenuKeyPressed() && !InteractionLock.IsEditingInspector)
            {
                if (IsMenuOpen) CloseMenu();
                else if (!InteractionLock.IsModalOpen) ShowMainPage();   // not on top of the help window
            }
            else if (IsMenuOpen && WasEscapePressed())
            {
                CloseMenu();
            }
        }

        /// <summary>Texts built in code don't update themselves — rebuild them.</summary>
        private void OnLanguageChanged()
        {
            if (checkManager.IsChecking && checkManager.LastResult != null)
                ShowBanner(checkManager.LastResult);

            if (!IsMenuOpen)
                return;
            if (mainPage != null && mainPage.activeSelf)
                ShowMainPage();
            else if (journalPage != null && journalPage.activeSelf)
                ShowJournalPage();
        }

        // ── Window & pages ────────────────────────────────────────────────────

        private void ShowPage(GameObject page)
        {
            if (menu == null)
                return;

            menu.SetActive(true);
            InteractionLock.SetModalOpen(true);

            SetActive(mainPage, page == mainPage);
            SetActive(savePage, page == savePage);
            SetActive(passwordPage, page == passwordPage);
            SetActive(journalPage, page == journalPage);
        }

        public void CloseMenu()
        {
            pendingReference = null;
            pendingPath = null;

            if (menu != null)
                menu.SetActive(false);
            InteractionLock.SetModalOpen(false);
            // A focused password field would otherwise keep the keyboard captured.
            InteractionLock.SetEditingInspector(false);
        }

        private void ShowMainPage()
        {
            ShowPage(mainPage);

            var checking = checkManager.IsChecking;
            if (exitCheckButton != null)
                exitCheckButton.gameObject.SetActive(checking);

            if (menuStatus != null)
            {
                var result = checkManager.LastResult;
                menuStatus.text = checking && result != null
                    ? Loc.Format("CHECK_MENU_STATUS_CHECKING", result.SchemaId, result.ModeName.ToLowerInvariant(), result.FileName)
                    : Loc.Get("CHECK_MENU_STATUS_IDLE");
            }
        }

        // ── Save ──────────────────────────────────────────────────────────────

        private void ShowSavePage()
        {
            ShowPage(savePage);
            if (savePasswordField != null)
                savePasswordField.SetTextWithoutNotify(string.Empty);
            SetSaveMode(SchemaMode.Training);
        }

        private void SetSaveMode(SchemaMode mode)
        {
            saveMode = mode;
            PaintToggle(trainingModeButton, mode == SchemaMode.Training);
            PaintToggle(examModeButton, mode == SchemaMode.Exam);
            SetActive(savePasswordGroup, mode == SchemaMode.Exam);
            SetError(saveError, null);

            if (mode == SchemaMode.Exam && savePasswordField != null)
                savePasswordField.ActivateInputField();
        }

        private void OnSaveConfirm()
        {
            if (savePage == null || !savePage.activeInHierarchy)
                return;

            var password = savePasswordField != null ? savePasswordField.text : string.Empty;
            if (saveMode == SchemaMode.Exam && password.Length < MinPasswordLength)
            {
                SetError(saveError, Loc.Format("SAVE_PASSWORD_TOO_SHORT", MinPasswordLength));
                return;
            }

            var mode = saveMode;
            CloseMenu();

            var path = FileDialogs.SaveFile(Loc.Get("SAVE_DIALOG_TITLE"), Loc.Get("SAVE_DEFAULT_FILE_NAME"), SchemaFile.Extension);
            if (path == null)
                return;

            checkManager.TrySaveReference(path, mode, password, out var message);
            EditorNotifications.Post(message);
        }

        // ── Load ──────────────────────────────────────────────────────────────

        private void OnLoad()
        {
            CloseMenu();

            var path = FileDialogs.OpenFile(Loc.Get("LOAD_DIALOG_TITLE"), SchemaFile.Extension);
            if (path == null)
                return;

            if (!checkManager.TryReadReference(path, out var reference, out var error))
            {
                EditorNotifications.Post(error);
                return;
            }

            if (reference.IsExam)
            {
                ShowPasswordPage(reference, path);
                return;
            }

            StartCheck(reference, path);
        }

        private void ShowPasswordPage(SubstationSchema reference, string path)
        {
            ShowPage(passwordPage);
            pendingReference = reference;
            pendingPath = path;

            if (passwordInfo != null)
                passwordInfo.text = Loc.Format("PASSWORD_INFO", reference.DisplayId);
            SetError(passwordError, null);

            if (loadPasswordField != null)
            {
                loadPasswordField.SetTextWithoutNotify(string.Empty);
                loadPasswordField.ActivateInputField();
            }
        }

        private void OnPasswordConfirm()
        {
            if (pendingReference == null || passwordPage == null || !passwordPage.activeInHierarchy)
                return;

            var password = loadPasswordField != null ? loadPasswordField.text : string.Empty;
            if (!pendingReference.CheckPassword(password))
            {
                checkManager.LogWrongPassword(pendingReference, pendingPath);
                SetError(passwordError, Loc.Get("PASSWORD_WRONG"));
                if (loadPasswordField != null)
                {
                    loadPasswordField.SetTextWithoutNotify(string.Empty);
                    loadPasswordField.ActivateInputField();
                }
                return;
            }

            var reference = pendingReference;
            var path = pendingPath;
            CloseMenu();
            StartCheck(reference, path);
        }

        private void StartCheck(SubstationSchema reference, string path)
        {
            var result = checkManager.StartCheck(reference, path);
            EditorNotifications.Post(result.IsPerfect
                ? Loc.Get("CHECK_RESULT_PERFECT")
                : Loc.Format("CHECK_RESULT_SCORE", result.CorrectWires.Count, result.ReferenceWires));
        }

        private void OnExitCheck()
        {
            CloseMenu();
            if (!checkManager.IsChecking)
                return;

            checkManager.EndCheck();
            EditorNotifications.Post(Loc.Get("CHECK_ENDED"));
        }

        // ── Journal ───────────────────────────────────────────────────────────

        private void ShowJournalPage()
        {
            ShowPage(journalPage);

            var entries = CheckJournal.Load();
            var warning = CheckJournal.TamperWarning;

            if (journalWarning != null)
            {
                journalWarning.gameObject.SetActive(!string.IsNullOrEmpty(warning));
                journalWarning.text = Loc.Get("JOURNAL_TAMPERED") + "\n" + warning;
            }

            if (journalText == null)
                return;

            if (entries.Count == 0)
            {
                journalText.text = Loc.Get("JOURNAL_EMPTY");
                return;
            }

            // Newest first
            var text = new StringBuilder();
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                text.Append("<color=#8F8F8F>").Append(e.time).Append("</color>   ")
                    .Append("<b>").Append(e.schemaId).Append("</b>   ")
                    .Append(e.mode).Append("   ")
                    .Append(e.fileName).Append('\n')
                    .Append("<color=#B8B8B8>      ").Append(e.outcome).Append("</color>\n");
            }
            journalText.text = text.ToString();
        }

        // ── Banner ────────────────────────────────────────────────────────────

        private void ShowBanner(CheckResult result)
        {
            if (banner == null)
                return;

            banner.SetActive(true);

            var correctHex = Hex(SubstationCheckManager.CorrectColor);
            var wrongHex = Hex(SubstationCheckManager.WrongColor);
            var missingHex = Hex(SubstationCheckManager.MissingColor);

            if (bannerTitle != null)
            {
                var title = Loc.Format("BANNER_TITLE", result.ModeName, result.SchemaId, result.FileName);
                bannerTitle.text = result.IsPerfect ? $"{title}: <color={correctHex}>{Loc.Get("BANNER_ALL_CORRECT")}</color>" : title;
            }

            if (bannerStats != null)
            {
                bannerStats.text =
                    $"<color={correctHex}>{Loc.Format("BANNER_CORRECT", result.CorrectWires.Count, result.ReferenceWires)}</color>     " +
                    $"<color={wrongHex}>{Loc.Format("BANNER_WRONG", result.WrongWires.Count)}</color>     " +
                    $"<color={missingHex}>{Loc.Format("BANNER_MISSING", result.MissingTotal)}</color>";
            }

            if (bannerDetails != null)
            {
                var details = new StringBuilder();
                if (result.MissingObjects.Count > 0)
                    details.Append(Loc.Format("BANNER_MISSING_OBJECTS", string.Join(", ", result.MissingObjects))).Append('\n');
                if (result.ExtraObjects.Count > 0)
                    details.Append(Loc.Format("BANNER_EXTRA_OBJECTS", string.Join(", ", result.ExtraObjects))).Append('\n');
                if (result.MissingWithoutObjects > 0)
                    details.Append(Loc.Format("BANNER_HIDDEN_WIRES", result.MissingWithoutObjects)).Append('\n');
                details.Append(Loc.Get("BANNER_RULES"));

                bannerDetails.text = details.ToString();
            }
        }

        private void HideBanner()
        {
            if (banner != null)
                banner.SetActive(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null)
                go.SetActive(active);
        }

        private static void SetError(TMP_Text label, string message)
        {
            if (label == null)
                return;
            label.gameObject.SetActive(!string.IsNullOrEmpty(message));
            label.text = message ?? string.Empty;
        }

        private static void PaintToggle(Button button, bool active)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor      = active ? UITheme.Accent : UITheme.Button;
            colors.highlightedColor = active ? UITheme.AccentBright : UITheme.ButtonHover;
            colors.selectedColor    = colors.normalColor;
            button.colors = colors;
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private static bool WasMenuKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.f2Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F2);
#endif
        }

        private static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
