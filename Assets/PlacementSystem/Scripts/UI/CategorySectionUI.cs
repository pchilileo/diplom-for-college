using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    public class CategorySectionUI : MonoBehaviour
    {
        [SerializeField] private Button headerButton;
        [SerializeField] private Text headerLabel;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private Text expandIndicator;

        private bool isExpanded = true;

        private void Awake()
        {
            if (headerButton != null)
                headerButton.onClick.AddListener(ToggleExpanded);

            SetExpanded(true, instant: true);
        }

        public void SetCategoryName(string name)
        {
            if (headerLabel != null)
                headerLabel.text = name;
        }

        public RectTransform ContentRoot => contentRoot != null ? contentRoot : (RectTransform)transform;
        public GridLayoutGroup Grid => grid;

        private void ToggleExpanded()
        {
            SetExpanded(!isExpanded, instant: false);
        }

        private void SetExpanded(bool expanded, bool instant)
        {
            isExpanded = expanded;
            if (contentRoot != null)
                contentRoot.gameObject.SetActive(expanded);

            if (expandIndicator != null)
                expandIndicator.text = expanded ? "▼" : "▶";
        }
    }
}
