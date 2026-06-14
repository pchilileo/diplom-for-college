using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    public class RightPanelController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Vector3FieldGroup positionFields;
        [SerializeField] private Vector3FieldGroup rotationFields;
        [SerializeField] private Vector3FieldGroup scaleFields;
        [SerializeField] private Button translateModeButton;
        [SerializeField] private Button rotateModeButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private RuntimeTransformGizmo transformGizmo;

        private PlacedObject boundObject;
        private bool suppressRefresh;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (positionFields != null)
            {
                positionFields.SetStep(0.1f);
                positionFields.Bind(ApplyPosition);
            }

            if (rotationFields != null)
            {
                rotationFields.SetStep(1f);
                rotationFields.Bind(ApplyRotation);
            }

            if (scaleFields != null)
            {
                scaleFields.SetStep(0.1f);
                scaleFields.BindScale(ApplyScale);
            }

            if (translateModeButton != null)
                translateModeButton.onClick.AddListener(() => SetGizmoMode(GizmoMode.Translate));

            if (rotateModeButton != null)
                rotateModeButton.onClick.AddListener(() => SetGizmoMode(GizmoMode.Rotate));

            if (deleteButton != null)
                deleteButton.onClick.AddListener(OnDeleteClicked);

            HidePanel();
        }

        // FIX: Use Start (instead of OnEnable) for the first subscription so that
        // SelectionManager.Instance is guaranteed to be initialised by the time we
        // subscribe. OnEnable fires before other Awakes in some scene orderings,
        // meaning Instance could still be null and the event never gets hooked up.
        private void Start()
        {
            SubscribeToSelection();
        }

        private void OnEnable()
        {
            // Re-subscribe after the component is re-enabled (e.g. toggled at runtime)
            // Start already handles the very first subscription, so guard with null check.
            if (SelectionManager.Instance != null)
                SubscribeToSelection();
        }

        private void OnDisable()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.SelectionChanged -= OnSelectionChanged;

            UnbindTransformEvents();
        }

        private void SubscribeToSelection()
        {
            if (SelectionManager.Instance == null)
                return;

            // Remove first to avoid double-subscription if called multiple times
            SelectionManager.Instance.SelectionChanged -= OnSelectionChanged;
            SelectionManager.Instance.SelectionChanged += OnSelectionChanged;
        }

        // ── Update ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (boundObject == null)
                return;

            RefreshFromObject();

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.deleteKey.wasPressedThisFrame)
                OnDeleteClicked();
#else
            if (Input.GetKeyDown(KeyCode.Delete))
                OnDeleteClicked();
#endif
        }

        // ── Selection ──────────────────────────────────────────────────────────

        private void OnSelectionChanged(PlacedObject selected)
        {
            UnbindTransformEvents();
            boundObject = selected;

            if (boundObject == null)
            {
                HidePanel();
                return;
            }

            ShowPanel();
            boundObject.TransformChanged += OnTargetTransformChanged;
            RefreshFromObject(force: true);
        }

        private void OnTargetTransformChanged(PlacedObject _)
        {
            RefreshFromObject(force: true);
        }

        private void UnbindTransformEvents()
        {
            if (boundObject != null)
                boundObject.TransformChanged -= OnTargetTransformChanged;
        }

        // ── Panel visibility ───────────────────────────────────────────────────

        private void ShowPanel()
        {
            if (panelGroup == null)
                return;

            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        private void HidePanel()
        {
            if (panelGroup == null)
                return;

            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        // ── Inspector refresh ──────────────────────────────────────────────────

        private void RefreshFromObject(bool force = false)
        {
            if (boundObject == null || suppressRefresh)
                return;

            if (!force && InteractionLock.IsEditingInspector)
                return;

            suppressRefresh = true;
            if (titleLabel != null)
                titleLabel.text = boundObject.SourceAsset != null
                    ? boundObject.SourceAsset.DisplayName
                    : boundObject.name;

            var transformRef = boundObject.transform;
            positionFields?.SetFromVector(transformRef.position);
            rotationFields?.SetFromVector(transformRef.rotation.eulerAngles);
            scaleFields?.SetFromVector(transformRef.localScale, uniformScale: true);
            suppressRefresh = false;
        }

        // ── Apply from inspector fields ────────────────────────────────────────

        private void ApplyPosition(Vector3 value)
        {
            if (boundObject == null || suppressRefresh)
                return;

            if (PlacementManager.Instance != null)
                value = PlacementManager.Instance.SnapSettings.SnapPosition(value);

            boundObject.transform.position = value;
            boundObject.NotifyTransformChanged();
        }

        private void ApplyRotation(Vector3 euler)
        {
            if (boundObject == null || suppressRefresh)
                return;

            if (PlacementManager.Instance != null)
                euler = PlacementManager.Instance.SnapSettings.SnapRotation(euler);

            boundObject.transform.rotation = Quaternion.Euler(euler);
            boundObject.NotifyTransformChanged();
        }

        private void ApplyScale(Vector3 value)
        {
            if (boundObject == null || suppressRefresh)
                return;

            var uniform = Mathf.Max(0.01f, value.x);
            boundObject.transform.localScale = Vector3.one * uniform;
            boundObject.NotifyTransformChanged();
        }

        private void SetGizmoMode(GizmoMode mode)
        {
            transformGizmo?.SetMode(mode);
        }

        private void OnDeleteClicked()
        {
            SelectionManager.Instance?.DeleteSelected();
        }
    }
}