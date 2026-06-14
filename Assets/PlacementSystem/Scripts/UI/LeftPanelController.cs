using UnityEngine;
using UnityEngine.UI;

namespace PlacementSystem
{
    public class LeftPanelController : MonoBehaviour
    {
        [SerializeField] private PlacementAssetDatabase database;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform categoryContainer;
        [SerializeField] private CategorySectionUI categorySectionPrefab;
        [SerializeField] private UIAssetSlot assetSlotPrefab;
        [SerializeField] private DragPlacementHandler dragHandler;
        [SerializeField] private Canvas rootCanvas;

        private void Start()
        {
            BuildLibrary();
        }

        public void SetDatabase(PlacementAssetDatabase assetDatabase)
        {
            database = assetDatabase;
            BuildLibrary();
        }

        private void BuildLibrary()
        {
            if (database == null || categoryContainer == null || categorySectionPrefab == null)
                return;

            for (var i = categoryContainer.childCount - 1; i >= 0; i--)
                Destroy(categoryContainer.GetChild(i).gameObject);

            foreach (var category in database.Categories)
            {
                if (category == null)
                    continue;

                var section = Instantiate(categorySectionPrefab, categoryContainer);
                section.SetCategoryName(category.CategoryName);

                var gridParent = section.ContentRoot;
                var grid = section.Grid;
                if (grid == null)
                {
                    var gridObject = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
                    gridObject.transform.SetParent(gridParent, false);
                    grid = gridObject.GetComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(96f, 110f);
                    grid.spacing = new Vector2(8f, 8f);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 2;
                }

                foreach (var asset in category.Assets)
                {
                    if (asset == null)
                        continue;

                    var slot = Instantiate(assetSlotPrefab, grid.transform);
                    slot.Bind(asset, dragHandler, rootCanvas);
                }
            }
        }
    }
}
