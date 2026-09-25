using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    /// <summary>
    /// One inspector row with X / Y / Z fields (Position, Rotation, Scale).
    ///
    /// • Type a value and press Enter (or click away) to apply it.
    /// • Drag an axis label left / right to scrub the value.
    /// • Rows with a link button can keep proportions: changing one axis
    ///   scales the other two by the same factor (like "Constrain Proportions"
    ///   on Scale in the Unity inspector).
    /// </summary>
    public class Vector3FieldGroup : MonoBehaviour
    {
        [Header("Fields")]
        [SerializeField] private TMP_InputField xField;
        [SerializeField] private TMP_InputField yField;
        [SerializeField] private TMP_InputField zField;

        [Header("Axis labels (drag to scrub)")]
        [SerializeField] private AxisScrubHandle xHandle;
        [SerializeField] private AxisScrubHandle yHandle;
        [SerializeField] private AxisScrubHandle zHandle;

        [Header("Proportional link (optional)")]
        [SerializeField] private Button linkButton;
        [SerializeField] private Graphic linkGraphic;
        [SerializeField] private bool linked = true;

        [Header("Settings")]
        [Tooltip("Value change per pixel of mouse movement while scrubbing a label.")]
        [SerializeField] private float scrubSpeed = 0.02f;
        [SerializeField] private float minValue = float.MinValue;
        [SerializeField] private string format = "0.##";

        private Vector3 current;
        private Action<Vector3> onValueChanged;

        private bool HasLink => linkButton != null;

        private void Awake()
        {
            HookField(xField, 0);
            HookField(yField, 1);
            HookField(zField, 2);

            HookHandle(xHandle, 0);
            HookHandle(yHandle, 1);
            HookHandle(zHandle, 2);

            if (linkButton != null)
                linkButton.onClick.AddListener(ToggleLink);

            UpdateLinkVisual();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Bind(Action<Vector3> onChanged)
        {
            onValueChanged = onChanged;
        }

        /// <summary>Shows <paramref name="value"/>. A field that is being edited is left alone.</summary>
        public void SetValue(Vector3 value)
        {
            current = value;
            WriteField(xField, value.x);
            WriteField(yField, value.y);
            WriteField(zField, value.z);
        }

        // ── Editing ───────────────────────────────────────────────────────────

        private void HookField(TMP_InputField field, int axis)
        {
            if (field == null)
                return;

            field.onEndEdit.AddListener(text => OnFieldEndEdit(field, axis, text));
        }

        private void HookHandle(AxisScrubHandle handle, int axis)
        {
            if (handle == null)
                return;

            handle.Scrubbed += pixels => SetAxis(axis, current[axis] + pixels * scrubSpeed);
        }

        private void OnFieldEndEdit(TMP_InputField field, int axis, string text)
        {
            InteractionLock.SetEditingInspector(false);

            if (TryParse(text, out var value))
                SetAxis(axis, value);
            else
                WriteField(field, current[axis], force: true);   // invalid input → show the old value
        }

        private void SetAxis(int axis, float value)
        {
            value = Mathf.Max(minValue, value);

            if (HasLink && linked)
            {
                var old = current[axis];
                if (Mathf.Abs(old) > 1e-5f)
                {
                    var ratio = value / old;
                    for (var i = 0; i < 3; i++)
                        current[i] = Mathf.Max(minValue, current[i] * ratio);
                    current[axis] = value;   // exactly what was typed, no rounding drift
                }
                else
                {
                    current = Vector3.one * value;
                }
            }
            else
            {
                current[axis] = value;
            }

            WriteField(xField, current.x, force: true);
            WriteField(yField, current.y, force: true);
            WriteField(zField, current.z, force: true);

            onValueChanged?.Invoke(current);
        }

        private void ToggleLink()
        {
            linked = !linked;
            UpdateLinkVisual();
        }

        private void UpdateLinkVisual()
        {
            if (linkButton == null)
                return;

            var colors = linkButton.colors;
            colors.normalColor = linked ? UITheme.Accent : UITheme.FieldBackground;
            colors.highlightedColor = linked ? UITheme.AccentBright : UITheme.Hover;
            colors.selectedColor = colors.normalColor;
            linkButton.colors = colors;

            if (linkGraphic != null)
                linkGraphic.color = linked ? Color.white : UITheme.TextDim;
        }

        // ── Text helpers ──────────────────────────────────────────────────────

        private void WriteField(TMP_InputField field, float value, bool force = false)
        {
            if (field == null)
                return;

            // Don't overwrite what the user is typing.
            if (field.isFocused && !force)
                return;

            field.SetTextWithoutNotify(value.ToString(format, CultureInfo.InvariantCulture));
        }

        private static bool TryParse(string text, out float value)
        {
            // Accept both "1.5" and "1,5".
            text = text?.Trim().Replace(',', '.');
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
