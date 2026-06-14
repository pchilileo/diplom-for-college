using UnityEngine;

namespace PlacementSystem
{
    [CreateAssetMenu(fileName = "AssetCategory", menuName = "Placement System/Asset Category")]
    public class AssetCategory : ScriptableObject
    {
        [SerializeField] private string categoryName = "Category";
        [SerializeField] private Sprite categoryIcon;
        [SerializeField] private AssetData[] assets = System.Array.Empty<AssetData>();

        public string CategoryName => categoryName;
        public Sprite CategoryIcon => categoryIcon;
        public AssetData[] Assets => assets;
    }
}
