using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    /// <summary>Collapsible inspector section: a clickable header with an arrow and a body.</summary>
    public class FoldoutSection : MonoBehaviour
    {
        [SerializeField] private Button headerButton;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private GameObject content;
        [SerializeField] private bool expanded = true;

        public bool IsExpanded => expanded;

        private void Awake()
        {
            if (headerButton != null)
                headerButton.onClick.AddListener(Toggle);

            Apply();
        }

        public void Toggle()
        {
            SetExpanded(!expanded);
        }

        public void SetExpanded(bool value)
        {
            expanded = value;
            Apply();
        }

        private void Apply()
        {
            if (content != null)
                content.SetActive(expanded);

            // The arrow sprite points down; turned +90° it points right.
            if (arrow != null)
                arrow.localEulerAngles = new Vector3(0f, 0f, expanded ? 0f : 90f);
        }
    }
}
