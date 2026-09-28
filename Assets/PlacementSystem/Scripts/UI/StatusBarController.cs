using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    /// <summary>
    /// Bottom bar: editor mode tabs (same as keys 1 / 2 / 3), hints for the
    /// current mode and short messages from <see cref="EditorNotifications"/>.
    /// </summary>
    public class StatusBarController : MonoBehaviour
    {
        [SerializeField] private EditorModeManager modeManager;

        [Header("Mode tabs")]
        [SerializeField] private Button normalModeButton;
        [SerializeField] private Button wireConnectModeButton;
        [SerializeField] private Button wireDeleteModeButton;

        [Header("Language")]
        [SerializeField] private Button languageButton;
        [SerializeField] private TMP_Text languageLabel;

        [Header("Text")]
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private float messageDuration = 3f;

        private float messageHideTime;

        private void Awake()
        {
            if (modeManager == null)
                modeManager = FindAnyObjectByType<EditorModeManager>();

            Hook(normalModeButton, EditorModeManager.EditorMode.Normal);
            Hook(wireConnectModeButton, EditorModeManager.EditorMode.WireConnect);
            Hook(wireDeleteModeButton, EditorModeManager.EditorMode.WireDelete);

            if (messageLabel != null)
                messageLabel.gameObject.SetActive(false);

            if (languageButton != null)
                languageButton.onClick.AddListener(Loc.NextLanguage);
        }

        private void OnEnable()
        {
            if (modeManager != null)
                modeManager.ModeChanged += OnModeChanged;
            EditorNotifications.MessagePosted += ShowMessage;
            Loc.LanguageChanged += OnLanguageChanged;

            OnLanguageChanged();
            OnModeChanged(modeManager != null ? modeManager.CurrentMode : EditorModeManager.EditorMode.Normal);
        }

        private void OnDisable()
        {
            if (modeManager != null)
                modeManager.ModeChanged -= OnModeChanged;
            EditorNotifications.MessagePosted -= ShowMessage;
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            if (languageLabel != null)
                languageLabel.text = Loc.Language.ToUpperInvariant();

            OnModeChanged(modeManager != null ? modeManager.CurrentMode : EditorModeManager.EditorMode.Normal);
        }

        private void Update()
        {
            if (messageLabel != null && messageLabel.gameObject.activeSelf && Time.unscaledTime >= messageHideTime)
                messageLabel.gameObject.SetActive(false);
        }

        private void Hook(Button button, EditorModeManager.EditorMode mode)
        {
            if (button != null)
                button.onClick.AddListener(() => modeManager?.SwitchTo(mode));
        }

        private void OnModeChanged(EditorModeManager.EditorMode mode)
        {
            Paint(normalModeButton, mode == EditorModeManager.EditorMode.Normal);
            Paint(wireConnectModeButton, mode == EditorModeManager.EditorMode.WireConnect);
            Paint(wireDeleteModeButton, mode == EditorModeManager.EditorMode.WireDelete);

            if (hintLabel != null)
            {
                hintLabel.text = mode switch
                {
                    EditorModeManager.EditorMode.WireConnect => Loc.Get("HINT_WIRE_CONNECT"),
                    EditorModeManager.EditorMode.WireDelete  => Loc.Get("HINT_WIRE_DELETE"),
                    _                                        => Loc.Get("HINT_NORMAL"),
                };
            }
        }

        public void ShowMessage(string message)
        {
            if (messageLabel == null)
                return;

            messageLabel.text = message;
            messageLabel.gameObject.SetActive(true);
            messageHideTime = Time.unscaledTime + messageDuration;
        }

        private static void Paint(Button button, bool active)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor      = active ? UITheme.Accent : Color.clear;
            colors.highlightedColor = active ? UITheme.AccentBright : UITheme.Hover;
            colors.selectedColor    = colors.normalColor;
            button.colors = colors;
        }
    }
}
