using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    /// <summary>Collapsible category in the equipment library (header + list of items).</summary>
    public class CategorySectionUI : MonoBehaviour
    {
        [SerializeField] private Button headerButton;
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private RectTransform contentRoot;

        private bool isExpanded = true;
        private bool isFiltering;

        public RectTransform ContentRoot => contentRoot != null ? contentRoot : (RectTransform)transform;

        private void Awake()
        {
            if (headerButton != null)
                headerButton.onClick.AddListener(ToggleExpanded);

            Apply();
        }

        public void SetCategory(string name, int itemCount)
        {
            if (headerLabel != null)
                headerLabel.text = name;
            if (countLabel != null)
                countLabel.text = itemCount.ToString();
        }

        /// <summary>
        /// While a search is active every section with matches is shown expanded;
        /// the expand / collapse state chosen by the user comes back when the search is cleared.
        /// </summary>
        public void SetFilter(bool filtering, int matchCount)
        {
            isFiltering = filtering;
            gameObject.SetActive(!filtering || matchCount > 0);

            if (countLabel != null)
                countLabel.text = matchCount.ToString();

            Apply();
        }

        private void ToggleExpanded()
        {
            if (isFiltering)
                return;

            isExpanded = !isExpanded;
            Apply();
        }

        private void Apply()
        {
            var expanded = isExpanded || isFiltering;

            if (contentRoot != null)
                contentRoot.gameObject.SetActive(expanded);

            // The arrow sprite points down; turned +90° it points right.
            if (arrow != null)
                arrow.localEulerAngles = new Vector3(0f, 0f, expanded ? 0f : 90f);
        }
    }
}
