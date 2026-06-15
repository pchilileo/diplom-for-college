using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    public class Vector3FieldGroup : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private InputField xField;
        [SerializeField] private InputField yField;
        [SerializeField] private InputField zField;

        [Header("Step Buttons")]
        [SerializeField] private Button xMinus;
        [SerializeField] private Button xPlus;
        [SerializeField] private Button yMinus;
        [SerializeField] private Button yPlus;
        [SerializeField] private Button zMinus;
        [SerializeField] private Button zPlus;

        [Header("Settings")]
        [SerializeField] private float step = 0.01f;
        [SerializeField] private float minScale = 0.01f;

        private bool useUniformScale;
        private bool suppressEvents;
        private System.Action<Vector3> onValueChanged;

        public void SetFromVector(Vector3 value, bool uniformScale = false)
        {
            if (suppressEvents)
                return;

            useUniformScale = uniformScale;

            suppressEvents = true;

            if (useUniformScale)
            {
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
        }

        public void Bind(System.Action<Vector3> onChanged)
        {
            useUniformScale = false;
            onValueChanged = onChanged;
            BindFields();
            BindStepButtons();
        }

        public void BindScale(System.Action<Vector3> onChanged)
        {
            useUniformScale = true;
            onValueChanged = onChanged;
            BindFields();
            BindStepButtons();
        }

        private void BindFields()
        {
            // Очищаем старые слушатели
            if (xField != null)
            {
                xField.onEndEdit.RemoveAllListeners();
                xField.onEndEdit.AddListener(_ => OnFieldEndEdit());
            }
            if (yField != null)
            {
                yField.onEndEdit.RemoveAllListeners();
                yField.onEndEdit.AddListener(_ => OnFieldEndEdit());
            }
            if (zField != null)
            {
                zField.onEndEdit.RemoveAllListeners();
                zField.onEndEdit.AddListener(_ => OnFieldEndEdit());
            }
        }

        private void OnFieldEndEdit()
        {
            if (suppressEvents)
                return;

            InteractionLock.SetEditingInspector(false);

            var value = ReadVector();

            if (useUniformScale)
            {
                // Для scale берем среднее арифметическое или значение X как основное
                var uniform = Mathf.Max(minScale, value.x);
                value = Vector3.one * uniform;
            }

            onValueChanged?.Invoke(value);
        }

        private void BindStepButtons()
        {
            ClearButtonListeners(xMinus);
            ClearButtonListeners(xPlus);
            ClearButtonListeners(yMinus);
            ClearButtonListeners(yPlus);
            ClearButtonListeners(zMinus);
            ClearButtonListeners(zPlus);

            if (xMinus != null) xMinus.onClick.AddListener(() => OnStepClick(0, -step));
            if (xPlus != null) xPlus.onClick.AddListener(() => OnStepClick(0, step));
            if (yMinus != null) yMinus.onClick.AddListener(() => OnStepClick(1, -step));
            if (yPlus != null) yPlus.onClick.AddListener(() => OnStepClick(1, step));
            if (zMinus != null) zMinus.onClick.AddListener(() => OnStepClick(2, -step));
            if (zPlus != null) zPlus.onClick.AddListener(() => OnStepClick(2, step));
        }

        private void ClearButtonListeners(Button button)
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
        }

        private void OnStepClick(int axis, float delta)
        {
            if (suppressEvents)
                return;

            var value = ReadVector();

            if (useUniformScale)
            {
                var uniform = Mathf.Max(minScale, value.x + delta);
                value = Vector3.one * uniform;
            }
            else
            {
                value[axis] += delta;
            }

            suppressEvents = true;
            SetField(xField, value.x);
            SetField(yField, value.y);
            SetField(zField, value.z);
            suppressEvents = false;

            onValueChanged?.Invoke(value);
        }

        private Vector3 ReadVector()
        {
            return new Vector3(
                ParseField(xField),
                ParseField(yField),
                ParseField(zField)
            );
        }

        private float ParseField(InputField field)
        {
            if (field == null || string.IsNullOrEmpty(field.text))
                return 0f;

            if (float.TryParse(field.text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
                return v;

            return 0f;
        }

        private void SetField(InputField field, float value)
        {
            if (field != null)
            {
                field.text = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}