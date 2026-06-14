using UnityEngine;

namespace PlacementSystem
{
    [CreateAssetMenu(fileName = "AssetData", menuName = "Placement System/Asset Data")]
    public class AssetData : ScriptableObject
    {
        [SerializeField] private string displayName = "Object";
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;
        [SerializeField] private AssetCategory categoryRef;

        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public Sprite Icon => icon;
        public AssetCategory CategoryRef => categoryRef;

        public void SetCategory(AssetCategory category)
        {
            categoryRef = category;
        }
    }
}
