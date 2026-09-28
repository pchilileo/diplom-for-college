using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    /// <summary>
    /// Help window with tabs (Ctrl+O, or the "?" button in the status bar).
    /// Opens by itself the very first time the trainer is started.
    ///
    /// The tabs come from the language files: HELP_TAB_1_TITLE / HELP_TAB_1_BODY,
    /// HELP_TAB_2_TITLE / HELP_TAB_2_BODY, … — numbering must be continuous.
    /// A new tab can be added by adding the next pair of keys to the .lang file.
    /// </summary>
    public class HelpWindowController : MonoBehaviour
    {
        private const string FirstRunKey = "PlacementSystem.HelpShown";
        private const int MaxTabs = 50;

        [SerializeField] private GameObject window;
        [SerializeField] private Button dimButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Optional button that opens the help (e.g. \"?\" in the status bar).")]
        [SerializeField] private Button openButton;

        [Header("Tabs")]
        [SerializeField] private RectTransform tabContainer;
        [Tooltip("Inactive button cloned for every tab.")]
        [SerializeField] private Button tabTemplate;

        [Header("Page")]
        [SerializeField] private TMP_Text pageTitle;
        [SerializeField] private TMP_Text pageBody;
        [SerializeField] private ScrollRect pageScroll;

        private readonly List<Button> tabButtons = new();
        private readonly List<string> tabKeys = new();   // "HELP_TAB_1", "HELP_TAB_2", …
        private int currentTab;

        public bool IsOpen => window != null && window.activeSelf;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Explicit null checks: "?." does not see Unity's fake-null objects.
            if (dimButton != null) dimButton.onClick.AddListener(() =>
            {
                if (!InteractionLock.AreClicksSuppressed)
                    Close();
            });
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (openButton != null) openButton.onClick.AddListener(Toggle);

            if (tabTemplate != null)
                tabTemplate.gameObject.SetActive(false);
            if (window != null)
                window.SetActive(false);
        }

        private void Start()
        {
            BuildTabs();

            // First launch: show the help once.
            if (!PlayerPrefs.HasKey(FirstRunKey))
            {
                PlayerPrefs.SetInt(FirstRunKey, 1);
                PlayerPrefs.Save();
                Open();
            }
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (IsOpen)
                Close();
        }

        private void Update()
        {
            if (WasHotkeyPressed() && !InteractionLock.IsEditingInspector)
            {
                // Don't open on top of another window (e.g. the F2 menu).
                if (IsOpen || !InteractionLock.IsModalOpen)
                    Toggle();
            }
            else if (IsOpen && WasEscapePressed())
            {
                InteractionLock.ConsumeEscape();
                Close();
            }
        }

        // ── Open / close ──────────────────────────────────────────────────────

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (window == null || InteractionLock.IsModalOpen && !IsOpen)
                return;

            window.SetActive(true);
            InteractionLock.SetModalOpen(true);
            SelectTab(currentTab);
        }

        public void Close()
        {
            if (window != null)
                window.SetActive(false);
            InteractionLock.SetModalOpen(false);
        }

        // ── Tabs ──────────────────────────────────────────────────────────────

        private void BuildTabs()
        {
            foreach (var button in tabButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }
            tabButtons.Clear();
            tabKeys.Clear();

            if (tabContainer == null || tabTemplate == null)
                return;

            for (var i = 1; i <= MaxTabs; i++)
            {
                var key = $"HELP_TAB_{i}";
                if (!Loc.Has(key + "_TITLE"))
                    break;

                var index = tabKeys.Count;
                var button = Instantiate(tabTemplate, tabContainer);
                button.name = key;
                button.gameObject.SetActive(true);
                button.onClick.AddListener(() => SelectTab(index));

                tabKeys.Add(key);
                tabButtons.Add(button);
            }

            RefreshTabLabels();
            currentTab = Mathf.Clamp(currentTab, 0, Mathf.Max(0, tabKeys.Count - 1));
        }

        private void RefreshTabLabels()
        {
            for (var i = 0; i < tabButtons.Count; i++)
            {
                var label = tabButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = Loc.Get(tabKeys[i] + "_TITLE");
            }
        }

        public void SelectTab(int index)
        {
            if (tabKeys.Count == 0)
                return;

            currentTab = Mathf.Clamp(index, 0, tabKeys.Count - 1);
            var key = tabKeys[currentTab];

            if (pageTitle != null)
                pageTitle.text = Loc.Get(key + "_TITLE");
            if (pageBody != null)
                pageBody.text = Loc.Get(key + "_BODY");

            for (var i = 0; i < tabButtons.Count; i++)
                Paint(tabButtons[i], i == currentTab);

            // Start reading from the top.
            if (pageScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                pageScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void OnLanguageChanged()
        {
            // Another language may have a different number of tabs.
            BuildTabs();
            if (IsOpen)
                SelectTab(currentTab);
        }

        private static void Paint(Button button, bool active)
        {
            var colors = button.colors;
            colors.normalColor      = active ? UITheme.Accent : Color.clear;
            colors.highlightedColor = active ? UITheme.AccentBright : UITheme.Hover;
            colors.selectedColor    = colors.normalColor;
            button.colors = colors;
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private static bool WasHotkeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.oKey.wasPressedThisFrame
                && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
#else
            return Input.GetKeyDown(KeyCode.O) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
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
