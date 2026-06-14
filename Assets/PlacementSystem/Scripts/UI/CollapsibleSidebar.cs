using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    public class CollapsibleSidebar : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleLabel;
        [SerializeField] private float animationSpeed = 8f;
        [SerializeField] private float hiddenOffset = -320f;

        private bool isOpen = true;
        private float targetX;

        private void Awake()
        {
            if (toggleButton != null)
                toggleButton.onClick.AddListener(Toggle);

            targetX = 0f;
            UpdateToggleLabel();
        }

        private void Update()
        {
            if (panel == null)
                return;

            var anchored = panel.anchoredPosition;
            anchored.x = Mathf.Lerp(anchored.x, targetX, Time.unscaledDeltaTime * animationSpeed);
            panel.anchoredPosition = anchored;
        }

        public void Toggle()
        {
            isOpen = !isOpen;
            targetX = isOpen ? 0f : hiddenOffset;
            UpdateToggleLabel();
        }

        private void UpdateToggleLabel()
        {
            if (toggleLabel != null)
                toggleLabel.text = isOpen ? "<" : ">";
        }
    }
}
