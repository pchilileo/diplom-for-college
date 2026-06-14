using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlacementSystem
{
    public class UIAssetSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text labelText;

        private AssetData assetData;
        private DragPlacementHandler dragHandler;
        private Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;

        public void Bind(AssetData data, DragPlacementHandler handler, Canvas rootCanvas)
        {
            assetData = data;
            dragHandler = handler;
            canvas = rootCanvas;
            rectTransform = GetComponent<RectTransform>();

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
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (assetData == null || dragHandler == null)
                return;

            canvasGroup.blocksRaycasts = false;
            dragHandler.BeginDrag(assetData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            dragHandler?.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            dragHandler?.EndDrag(eventData);
        }
    }
}
