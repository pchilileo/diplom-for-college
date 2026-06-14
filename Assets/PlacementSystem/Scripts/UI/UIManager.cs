using UnityEngine;

namespace PlacementSystem
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private LeftPanelController leftPanel;
        [SerializeField] private RightPanelController rightPanel;
        [SerializeField] private PlacementAssetDatabase database;

        public LeftPanelController LeftPanel => leftPanel;
        public RightPanelController RightPanel => rightPanel;

        private void Awake()
        {
            if (leftPanel != null && database != null)
                leftPanel.SetDatabase(database);
        }
    }
}
