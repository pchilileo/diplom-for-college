using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    public class Vector3FieldGroup : MonoBehaviour
    {
        [SerializeField] private InputField xField;
        [SerializeField] private InputField yField;
        [SerializeField] private InputField zField;

        // +/- buttons for each axis.
        // Assign these in the Inspector on each Vector3FieldGroup component:
        //   Position group  → X/Y/Z minus+plus buttons
        //   Rotation group  → X/Y/Z minus+plus buttons
        //   Scale group     → only X minus+plus are needed (uniform), Y/Z can be left empty
        [SerializeField] private Button xMinus;
        [SerializeField] private Button xPlus;
        [SerializeField] private Button yMinus;
        [SerializeField] private Button yPlus;
        [SerializeField] private Button zMinus;
        [SerializeField] private Button zPlus;

        [SerializeField] private float step = 0.1f;

        // Whether this group should treat all three axes as a single uniform value.
        // Set via BindScale() — do NOT share this flag with position/rotation groups.
        private bool useUniformScale;

        private bool suppressEvents;

        // ── Public API ─────────────────────────────────────────────────────────

        public void SetStep(float newStep) => step = newStep;

        /// <summary>
        /// Write a vector into the three fields without triggering callbacks.
        /// Pass <c>uniformScale = true</c> only for the scale group.
        /// </summary>
        public void SetFromVector(Vector3 value, bool uniformScale = false)
        {
            // Never let a call from outside force uniformScale on a non-scale group.
            // Only honour the flag if this group was already set up as uniform-scale.
            var wasUniform = useUniformScale;
            if (uniformScale)
                useUniformScale = true;

            suppressEvents = true;

            if (useUniformScale)
            {
                // Display the same value in all three fields so the user sees a
                // single consistent number, but only X is the "authoritative" input.
                var uniform = value.x;
                SetField(xField, uniform);
                SetField(yField, uniform);
                SetField(zField, uniform);
            }
            else
            {
                SetField(xField, value.x);
                SetField(yField, value.y);
                SetField(zField, value.z);
            }

            suppressEvents = false;

            // Restore flag if it was false before (so a stray call with uniformScale=true
            // doesn't permanently flip a position/rotation group).
            if (!wasUniform)
                useUniformScale = false;
        }

        /// <summary>Bind all three fields and their +/− buttons (position / rotation).</summary>
        public void Bind(System.Action<Vector3> onChanged)
        {
            useUniformScale = false;
            BindFields(onChanged);
            BindStepButton(xMinus, 0, -step, onChanged);
            BindStepButton(xPlus,  0,  step, onChanged);
            BindStepButton(yMinus, 1, -step, onChanged);
            BindStepButton(yPlus,  1,  step, onChanged);
            BindStepButton(zMinus, 2, -step, onChanged);
            BindStepButton(zPlus,  2,  step, onChanged);
        }

        /// <summary>
        /// Bind as a uniform-scale group.
        /// Only the X field / X +/- buttons are the real controls;
        /// Y and Z fields are kept in sync for display only.
        /// </summary>
        public void BindScale(System.Action<Vector3> onChanged)
        {
            useUniformScale = true;
            BindFields(onChanged);

            // For scale we only use the X buttons; Y/Z buttons are optional extras.
            // We still wire them up — but they all modify the uniform value.
            BindStepButton(xMinus, 0, -step, onChanged);
            BindStepButton(xPlus,  0,  step, onChanged);
            BindStepButton(yMinus, 0, -step, onChanged); // axis 0 intentional: uniform
            BindStepButton(yPlus,  0,  step, onChanged);
            BindStepButton(zMinus, 0, -step, onChanged);
            BindStepButton(zPlus,  0,  step, onChanged);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void BindFields(System.Action<Vector3> onChanged)
        {
            if (useUniformScale)
            {
                // Only X field drives the value; Y and Z are read-only mirrors.
                BindField(xField, onChanged, isUniformDriver: true);
                // Y and Z fields still fire the callback so the user CAN type in them,
                // but we read only x from the result (ApplyScale in RightPanelController
                // already takes value.x as the uniform).
                BindField(yField, onChanged, isUniformDriver: false);
                BindField(zField, onChanged, isUniformDriver: false);
            }
            else
            {
                BindField(xField, onChanged, isUniformDriver: false);
                BindField(yField, onChanged, isUniformDriver: false);
                BindField(zField, onChanged, isUniformDriver: false);
            }
        }

        private void BindField(InputField field, System.Action<Vector3> onChanged, bool isUniformDriver)
        {
            if (field == null)
                return;

            field.onValueChanged.AddListener(_ =>
            {
                if (suppressEvents)
                    return;

                InteractionLock.SetEditingInspector(true);

                if (useUniformScale && isUniformDriver)
                {
                    // Sync the other two display fields silently
                    if (float.TryParse(field.text, out var v))
                    {
                        suppressEvents = true;
                        SetField(yField, v);
                        SetField(zField, v);
                        suppressEvents = false;
                    }
                }

                onChanged?.Invoke(ReadVector());
            });

            field.onEndEdit.AddListener(_ => InteractionLock.SetEditingInspector(false));
        }

        private void BindStepButton(Button button, int axis, float delta,
                                    System.Action<Vector3> onChanged)
        {
            if (button == null)
                return;

            button.onClick.AddListener(() =>
            {
                var value = ReadVector();

                if (useUniformScale)
                {
                    var uniform = Mathf.Max(0.01f, value.x + delta);
                    value = Vector3.one * uniform;
                }
                else
                {
                    value[axis] += delta;
                }

                // Write back into fields without re-triggering callbacks
                suppressEvents = true;
                SetField(xField, value.x);
                SetField(yField, value.y);
                SetField(zField, value.z);
                suppressEvents = false;

                onChanged?.Invoke(value);
            });
        }

        private Vector3 ReadVector()
        {
            return new Vector3(Parse(xField), Parse(yField), Parse(zField));
        }

        private static float Parse(InputField field)
        {
            if (field == null || !float.TryParse(field.text, out var v))
                return 0f;
            return v;
        }

        private static void SetField(InputField field, float value)
        {
            if (field != null)
                field.text = value.ToString("0.###");
        }
    }
}