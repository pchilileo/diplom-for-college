using TMPro;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Shows the text with <see cref="key"/> from the language files and updates
    /// it when the language changes. <see cref="prefix"/> / <see cref="suffix"/>
    /// are added as-is (e.g. a grey hotkey hint "  &lt;color=#8F8F8F&gt;T&lt;/color&gt;").
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private string prefix;
        [SerializeField] private string suffix;

        private TMP_Text label;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                Refresh();
            }
        }

        public void Setup(string newKey, string newPrefix = null, string newSuffix = null)
        {
            key = newKey;
            prefix = newPrefix;
            suffix = newSuffix;
            Refresh();
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= Refresh;
        }

        public void Refresh()
        {
            if (label == null)
                label = GetComponent<TMP_Text>();
            if (label != null && !string.IsNullOrEmpty(key))
                label.text = prefix + Loc.Get(key) + suffix;
        }
    }
}
