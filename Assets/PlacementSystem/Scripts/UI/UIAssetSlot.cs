using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlacementSystem
{
    /// <summary>One item of the equipment library. Drag it into the scene to place the object.</summary>
    public class UIAssetSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text labelText;

        private AssetData assetData;
        private DragPlacementHandler dragHandler;
        private CanvasGroup canvasGroup;
        private bool isHovered;
        private bool isDragging;

        public AssetData Asset => assetData;

        public void Bind(AssetData data, DragPlacementHandler handler)
        {
            assetData = data;
            dragHandler = handler;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (iconImage != null)
            {
                iconImage.sprite = data.Icon;
                iconImage.enabled = data.Icon != null;
            }

            if (labelText != null)
                labelText.text = data.DisplayName;

            name = $"Slot_{data.DisplayName}";
            UpdateBackground();
        }

        /// <summary>Case-insensitive match against the item name.</summary>
        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || assetData == null)
                return true;

            var name = assetData.DisplayName;
            return !string.IsNullOrEmpty(name)
                && name.IndexOf(query.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── Drag into the scene ───────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (assetData == null || dragHandler == null)
                return;

            isDragging = true;
            canvasGroup.blocksRaycasts = false;
            dragHandler.BeginDrag(assetData);
            UpdateBackground();
        }

        public void OnDrag(PointerEventData eventData)
        {
            dragHandler?.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            canvasGroup.blocksRaycasts = true;
            dragHandler?.EndDrag(eventData);
            UpdateBackground();
        }

        // ── Hover highlight ───────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateBackground();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateBackground();
        }

        private void UpdateBackground()
        {
            if (background == null)
                return;

            background.color = isDragging ? UITheme.Accent
                : isHovered ? UITheme.Hover
                : Color.clear;
        }
    }
}
