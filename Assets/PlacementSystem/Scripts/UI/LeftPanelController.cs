using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>Equipment library: categories with their items, plus a search field.</summary>
    public class LeftPanelController : MonoBehaviour
    {
        [SerializeField] private PlacementAssetDatabase database;
        [SerializeField] private RectTransform categoryContainer;
        [SerializeField] private CategorySectionUI categorySectionPrefab;
        [SerializeField] private UIAssetSlot assetSlotPrefab;
        [SerializeField] private DragPlacementHandler dragHandler;
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private GameObject noResultsLabel;

        private readonly List<(CategorySectionUI section, List<UIAssetSlot> slots)> sections = new();
        private bool isBuilt;

        private void Awake()
        {
            if (searchField != null)
                searchField.onValueChanged.AddListener(ApplyFilter);
        }

        private void Start()
        {
            if (!isBuilt)
                BuildLibrary();
        }

        public void SetDatabase(PlacementAssetDatabase assetDatabase)
        {
            database = assetDatabase;
            BuildLibrary();
        }

        private void BuildLibrary()
        {
            if (database == null || categoryContainer == null || categorySectionPrefab == null || assetSlotPrefab == null)
                return;

            isBuilt = true;
            sections.Clear();

            for (var i = categoryContainer.childCount - 1; i >= 0; i--)
                Destroy(categoryContainer.GetChild(i).gameObject);

            foreach (var category in database.Categories)
            {
                if (category == null)
                    continue;

                var section = Instantiate(categorySectionPrefab, categoryContainer);
                section.name = $"Category_{category.CategoryName}";

                var slots = new List<UIAssetSlot>();
                foreach (var asset in category.Assets)
                {
                    if (asset == null)
                        continue;

                    var slot = Instantiate(assetSlotPrefab, section.ContentRoot);
                    slot.Bind(asset, dragHandler);
                    slots.Add(slot);
                }

                section.SetCategory(category.CategoryName, slots.Count);
                sections.Add((section, slots));
            }

            ApplyFilter(searchField != null ? searchField.text : string.Empty);
        }

        private void ApplyFilter(string query)
        {
            var filtering = !string.IsNullOrWhiteSpace(query);
            var totalMatches = 0;

            foreach (var (section, slots) in sections)
            {
                var matches = 0;
                foreach (var slot in slots)
                {
                    var visible = slot.Matches(query);
                    slot.gameObject.SetActive(visible);
                    if (visible)
                        matches++;
                }

                section.SetFilter(filtering, matches);
                totalMatches += matches;
            }

            if (noResultsLabel != null)
                noResultsLabel.SetActive(filtering && totalMatches == 0);
        }
    }
}
