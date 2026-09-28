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
    /// UI of the wiring check:
    /// • F2 opens a window: save the reference / load and check / leave the check.
    /// • While checking, a banner at the top shows the result and an exit button.
    /// </summary>
    public class CheckModeController : MonoBehaviour
    {
        [SerializeField] private SubstationCheckManager checkManager;

        [Header("Menu window")]
        [SerializeField] private GameObject menu;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button exitCheckButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Dark background behind the window: a click outside the window closes it.")]
        [SerializeField] private Button dimButton;
        [SerializeField] private TMP_Text menuStatus;

        [Header("Check banner")]
        [SerializeField] private GameObject banner;
        [SerializeField] private TMP_Text bannerTitle;
        [SerializeField] private TMP_Text bannerStats;
        [SerializeField] private TMP_Text bannerDetails;
        [SerializeField] private Button bannerExitButton;

        private static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        private void Awake()
        {
            if (checkManager == null)
                checkManager = FindAnyObjectByType<SubstationCheckManager>();
            if (checkManager == null)
                checkManager = gameObject.AddComponent<SubstationCheckManager>();

            // Explicit null checks: "?." does not see Unity's fake-null objects.
            Listen(saveButton, OnSave);
            Listen(loadButton, OnLoad);
            Listen(exitCheckButton, OnExitCheck);
            Listen(closeButton, CloseMenu);
            Listen(dimButton, CloseMenu);
            Listen(bannerExitButton, OnExitCheck);

            if (menu != null)
                menu.SetActive(false);
            if (banner != null)
                banner.SetActive(false);
        }

        private void OnEnable()
        {
            checkManager.CheckStarted += ShowBanner;
            checkManager.CheckEnded += HideBanner;
        }

        private void OnDisable()
        {
            checkManager.CheckStarted -= ShowBanner;
            checkManager.CheckEnded -= HideBanner;
            if (IsMenuOpen)
                CloseMenu();
        }

        private bool IsMenuOpen => menu != null && menu.activeSelf;

        private void Update()
        {
            if (WasMenuKeyPressed() && !InteractionLock.IsEditingInspector)
            {
                if (IsMenuOpen) CloseMenu();
                else OpenMenu();
            }
            else if (IsMenuOpen && WasEscapePressed())
            {
                CloseMenu();
            }
        }

        // ── Menu ──────────────────────────────────────────────────────────────

        public void OpenMenu()
        {
            if (menu == null)
                return;

            menu.SetActive(true);
            InteractionLock.SetModalOpen(true);

            var checking = checkManager.IsChecking;
            if (exitCheckButton != null)
                exitCheckButton.gameObject.SetActive(checking);

            if (menuStatus != null)
            {
                menuStatus.text = checking
                    ? $"Идёт проверка по файлу «{checkManager.LastResult?.FileName}»."
                    : "Сохраните эталон, чтобы по нему проверяли другие, или загрузите эталон и проверьте свою схему.";
            }
        }

        public void CloseMenu()
        {
            if (menu != null)
                menu.SetActive(false);
            InteractionLock.SetModalOpen(false);
        }

        private void OnSave()
        {
            CloseMenu();

            var path = FileDialogs.SaveFile("Сохранить эталон схемы", "Подстанция", SchemaFile.Extension);
            if (path == null)
                return;

            checkManager.TrySaveReference(path, out var message);
            EditorNotifications.Post(message);
        }

        private void OnLoad()
        {
            CloseMenu();

            var path = FileDialogs.OpenFile("Загрузить эталон для проверки", SchemaFile.Extension);
            if (path == null)
                return;

            checkManager.TryStartCheck(path, out var message);
            EditorNotifications.Post(message);
        }

        private void OnExitCheck()
        {
            CloseMenu();
            if (!checkManager.IsChecking)
                return;

            checkManager.EndCheck();
            EditorNotifications.Post("Проверка завершена — редактирование снова доступно");
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
                bannerTitle.text = result.IsPerfect
                    ? $"Проверка по «{result.FileName}»: <color={correctHex}>всё верно</color>"
                    : $"Проверка по «{result.FileName}»";
            }

            if (bannerStats != null)
            {
                bannerStats.text =
                    $"<color={correctHex}>Верно: {result.CorrectWires.Count} из {result.ReferenceWires}</color>     " +
                    $"<color={wrongHex}>Ошибочных: {result.WrongWires.Count}</color>     " +
                    $"<color={missingHex}>Не подключено: {result.MissingTotal}</color>";
            }

            if (bannerDetails != null)
            {
                var details = new StringBuilder();
                if (result.MissingObjects.Count > 0)
                    details.Append("Не хватает оборудования: ").Append(string.Join(", ", result.MissingObjects)).Append('\n');
                if (result.ExtraObjects.Count > 0)
                    details.Append("Лишнее оборудование: ").Append(string.Join(", ", result.ExtraObjects)).Append('\n');
                if (result.MissingWithoutObjects > 0)
                    details.Append($"Проводов, которые нельзя показать (нет оборудования): {result.MissingWithoutObjects}\n");
                details.Append("Провода менять нельзя; объекты можно двигать, поворачивать и масштабировать.");

                bannerDetails.text = details.ToString();
            }
        }

        private void HideBanner()
        {
            if (banner != null)
                banner.SetActive(false);
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
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
