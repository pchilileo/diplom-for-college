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

        [Header("Text")]
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private float messageDuration = 3f;

        private float messageHideTime;

        private const string NormalHint =
            "ЛКМ — выделить   ·   Перетащить из списка — поставить   ·   ПКМ + WASD — камера   ·   T / R — перемещение / поворот   ·   Del — удалить";
        private const string WireConnectHint =
            "ЛКМ по точке — начать / закончить провод   ·   Esc — отменить";
        private const string WireDeleteHint =
            "ЛКМ по проводу — удалить   ·   Esc — выйти";

        private void Awake()
        {
            if (modeManager == null)
                modeManager = FindAnyObjectByType<EditorModeManager>();

            Hook(normalModeButton, EditorModeManager.EditorMode.Normal);
            Hook(wireConnectModeButton, EditorModeManager.EditorMode.WireConnect);
            Hook(wireDeleteModeButton, EditorModeManager.EditorMode.WireDelete);

            if (messageLabel != null)
                messageLabel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (modeManager != null)
                modeManager.ModeChanged += OnModeChanged;
            EditorNotifications.MessagePosted += ShowMessage;

            OnModeChanged(modeManager != null ? modeManager.CurrentMode : EditorModeManager.EditorMode.Normal);
        }

        private void OnDisable()
        {
            if (modeManager != null)
                modeManager.ModeChanged -= OnModeChanged;
            EditorNotifications.MessagePosted -= ShowMessage;
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
                    EditorModeManager.EditorMode.WireConnect => WireConnectHint,
                    EditorModeManager.EditorMode.WireDelete  => WireDeleteHint,
                    _                                        => NormalHint,
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
