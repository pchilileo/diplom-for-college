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
    /// Inspector panel for the selected object: name, Transform (position of
    /// the bottom centre, rotation, scale), gizmo tool and connection info.
    /// </summary>
    public class RightPanelController : MonoBehaviour
    {
        [Header("States")]
        [SerializeField] private GameObject content;
        [SerializeField] private GameObject emptyState;

        [Header("Header")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private Image iconImage;

        [Header("Transform")]
        [SerializeField] private Vector3FieldGroup positionFields;
        [SerializeField] private Vector3FieldGroup rotationFields;
        [SerializeField] private Vector3FieldGroup scaleFields;
        [SerializeField] private Button resetButton;

        [Header("Tool")]
        [SerializeField] private Button translateModeButton;
        [SerializeField] private Button rotateModeButton;
        [SerializeField] private Button scaleModeButton;
        [SerializeField] private RuntimeTransformGizmo transformGizmo;

        [Header("Connections")]
        [SerializeField] private TMP_Text connectorsValue;
        [SerializeField] private TMP_Text wiresValue;

        [Header("Actions")]
        [SerializeField] private Button deleteButton;

        private PlacedObject boundObject;
        private readonly HashSet<WireConnection> wireBuffer = new();
        private int shownConnectors = -1;
        private int shownWires = -1;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            positionFields?.Bind(ApplyPosition);
            rotationFields?.Bind(ApplyRotation);
            scaleFields?.Bind(ApplyScale);

            if (resetButton != null)
                resetButton.onClick.AddListener(ResetRotationAndScale);

            if (translateModeButton != null)
                translateModeButton.onClick.AddListener(() => transformGizmo?.SetMode(GizmoMode.Translate));

            if (rotateModeButton != null)
                rotateModeButton.onClick.AddListener(() => transformGizmo?.SetMode(GizmoMode.Rotate));

            if (scaleModeButton != null)
                scaleModeButton.onClick.AddListener(() => transformGizmo?.SetMode(GizmoMode.Scale));

            if (deleteButton != null)
                deleteButton.onClick.AddListener(OnDeleteClicked);

            if (transformGizmo == null)
                transformGizmo = FindAnyObjectByType<RuntimeTransformGizmo>();

            ShowObject(null);
        }

        // Start (not OnEnable) for the first subscription: SelectionManager.Instance
        // is guaranteed to be initialised by then.
        private void Start()
        {
            SubscribeToSelection();

            if (transformGizmo != null)
            {
                transformGizmo.ModeChanged -= OnGizmoModeChanged;
                transformGizmo.ModeChanged += OnGizmoModeChanged;
                OnGizmoModeChanged(transformGizmo.Mode);
            }
        }

        private void OnEnable()
        {
            if (SelectionManager.Instance != null)
                SubscribeToSelection();
        }

        private void OnDisable()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.SelectionChanged -= ShowObject;
        }

        private void OnDestroy()
        {
            if (transformGizmo != null)
                transformGizmo.ModeChanged -= OnGizmoModeChanged;
        }

        private void SubscribeToSelection()
        {
            if (SelectionManager.Instance == null)
                return;

            SelectionManager.Instance.SelectionChanged -= ShowObject;
            SelectionManager.Instance.SelectionChanged += ShowObject;
        }

        private void Update()
        {
            if (boundObject == null)
                return;

            // Gizmo drags, undo-less edits etc. — just mirror the transform every frame.
            RefreshValues();

            if (!InteractionLock.IsEditingInspector && WasDeletePressed())
                OnDeleteClicked();
        }

        // ── Binding ────────────────────────────────────────────────────────────

        private void ShowObject(PlacedObject selected)
        {
            boundObject = selected;
            var hasObject = boundObject != null;

            if (content != null)
                content.SetActive(hasObject);
            if (emptyState != null)
                emptyState.SetActive(!hasObject);
            if (deleteButton != null)
                deleteButton.gameObject.SetActive(hasObject);

            if (!hasObject)
                return;

            var asset = boundObject.SourceAsset;

            if (titleLabel != null)
                titleLabel.text = asset != null ? asset.DisplayName : boundObject.name;

            if (subtitleLabel != null)
                subtitleLabel.text = asset != null && asset.CategoryRef != null
                    ? asset.CategoryRef.CategoryName
                    : string.Empty;

            if (iconImage != null)
            {
                iconImage.sprite = asset != null ? asset.Icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            RefreshValues();
        }

        private void RefreshValues()
        {
            if (boundObject == null)
                return;

            // While the user types or scrubs, the field owns the value.
            if (!InteractionLock.IsEditingInspector)
            {
                var t = boundObject.transform;
                positionFields?.SetValue(boundObject.PivotPoint);
                rotationFields?.SetValue(t.eulerAngles);
                scaleFields?.SetValue(t.localScale);
            }

            RefreshConnections();
        }

        private void RefreshConnections()
        {
            var connectors = boundObject.Connectors;

            wireBuffer.Clear();
            foreach (var connector in connectors)
            {
                if (connector == null)
                    continue;
                foreach (var wire in connector.Connections)
                {
                    if (wire != null)
                        wireBuffer.Add(wire);
                }
            }

            // Only touch the labels when a number changes (no per-frame strings / canvas rebuilds).
            if (connectorsValue != null && connectors.Count != shownConnectors)
            {
                shownConnectors = connectors.Count;
                connectorsValue.text = shownConnectors.ToString();
            }
            if (wiresValue != null && wireBuffer.Count != shownWires)
            {
                shownWires = wireBuffer.Count;
                wiresValue.text = shownWires.ToString();
            }
        }

        // ── Apply from inspector fields ────────────────────────────────────────

        private void ApplyPosition(Vector3 value)
        {
            if (boundObject == null)
                return;

            if (PlacementManager.Instance != null)
                value = PlacementManager.Instance.SnapSettings.SnapPosition(value);

            boundObject.SetPivotPosition(value);
            boundObject.NotifyTransformChanged();
        }

        private void ApplyRotation(Vector3 euler)
        {
            if (boundObject == null)
                return;

            if (PlacementManager.Instance != null)
                euler = PlacementManager.Instance.SnapSettings.SnapRotation(euler);

            boundObject.SetRotationAroundPivot(Quaternion.Euler(euler));
            boundObject.NotifyTransformChanged();
        }

        private void ApplyScale(Vector3 value)
        {
            if (boundObject == null)
                return;

            const float min = 0.01f;
            value = new Vector3(Mathf.Max(min, value.x), Mathf.Max(min, value.y), Mathf.Max(min, value.z));

            boundObject.SetScaleAroundPivot(value);
            boundObject.NotifyTransformChanged();
        }

        /// <summary>Back to the rotation and scale the model has in its prefab.</summary>
        private void ResetRotationAndScale()
        {
            if (boundObject == null)
                return;

            var prefab = boundObject.SourceAsset != null ? boundObject.SourceAsset.Prefab : null;
            var rotation = prefab != null ? prefab.transform.rotation : Quaternion.identity;
            var scale = prefab != null ? prefab.transform.localScale : Vector3.one;

            boundObject.SetScaleAroundPivot(scale);
            boundObject.SetRotationAroundPivot(rotation);
            boundObject.NotifyTransformChanged();
        }

        // ── Tool buttons ───────────────────────────────────────────────────────

        private void OnGizmoModeChanged(GizmoMode mode)
        {
            PaintToolButton(translateModeButton, mode == GizmoMode.Translate);
            PaintToolButton(rotateModeButton, mode == GizmoMode.Rotate);
            PaintToolButton(scaleModeButton, mode == GizmoMode.Scale);
        }

        private static void PaintToolButton(Button button, bool active)
        {
            if (button == null)
                return;

            var colors = button.colors;
            colors.normalColor      = active ? UITheme.Accent : UITheme.Button;
            colors.highlightedColor = active ? UITheme.AccentBright : UITheme.ButtonHover;
            colors.selectedColor    = colors.normalColor;
            button.colors = colors;
        }

        private void OnDeleteClicked()
        {
            SelectionManager.Instance?.DeleteSelected();
        }

        private static bool WasDeletePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.deleteKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Delete);
#endif
        }
    }
}
