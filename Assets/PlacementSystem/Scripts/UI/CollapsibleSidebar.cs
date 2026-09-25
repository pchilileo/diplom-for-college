using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    /// <summary>
    /// Slides a side panel out of the screen and back. The toggle tab is a child
    /// of the panel, so it stays at the screen edge while the panel is hidden.
    /// </summary>
    public class CollapsibleSidebar : MonoBehaviour
    {
        public enum Side { Left, Right }

        [SerializeField] private RectTransform panel;
        [SerializeField] private Button toggleButton;
        [SerializeField] private RectTransform toggleArrow;
        [SerializeField] private Side side = Side.Left;
        [SerializeField] private float animationSpeed = 12f;

        private bool isOpen = true;
        private float openX;
        private float targetX;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (panel == null)
                panel = (RectTransform)transform;

            if (toggleButton != null)
                toggleButton.onClick.AddListener(Toggle);

            openX = panel.anchoredPosition.x;
            targetX = openX;
            UpdateArrow();
        }

        private void Update()
        {
            var anchored = panel.anchoredPosition;
            if (Mathf.Approximately(anchored.x, targetX))
                return;

            anchored.x = Mathf.Lerp(anchored.x, targetX, Time.unscaledDeltaTime * animationSpeed);
            if (Mathf.Abs(anchored.x - targetX) < 0.5f)
                anchored.x = targetX;
            panel.anchoredPosition = anchored;
        }

        public void Toggle()
        {
            SetOpen(!isOpen);
        }

        public void SetOpen(bool open)
        {
            isOpen = open;
            var width = panel.rect.width;
            targetX = open ? openX : openX + (side == Side.Left ? -width : width);
            UpdateArrow();
        }

        private void UpdateArrow()
        {
            if (toggleArrow == null)
                return;

            // The arrow sprite points down: -90° turns it left, +90° right.
            var pointLeft = (side == Side.Left) == isOpen;
            toggleArrow.localEulerAngles = new Vector3(0f, 0f, pointLeft ? -90f : 90f);
        }
    }
}
