using UnityEngine;

namespace PlacementSystem
{
    [CreateAssetMenu(fileName = "PlacementAssetDatabase", menuName = "Placement System/Asset Database")]
    public class PlacementAssetDatabase : ScriptableObject
    {
        [SerializeField] private AssetCategory[] categories = System.Array.Empty<AssetCategory>();

        public AssetCategory[] Categories => categories;
    }
}
