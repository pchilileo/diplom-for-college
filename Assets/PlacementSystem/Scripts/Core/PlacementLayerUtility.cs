using UnityEngine;

namespace PlacementSystem
{
    public static class PlacementLayerUtility
    {
        public const string GroundLayerName = "Ground";

        private static int? groundLayerMask;

        public static int GroundLayer
        {
            get
            {
                var layer = LayerMask.NameToLayer(GroundLayerName);
                return layer >= 0 ? layer : 0;
            }
        }

        public static int GroundLayerMask
        {
            get
            {
                groundLayerMask ??= 1 << GroundLayer;
                return groundLayerMask.Value;
            }
        }

        public static bool TryRaycastGround(Ray ray, out RaycastHit hit, float maxDistance = 1000f)
        {
            return Physics.Raycast(ray, out hit, maxDistance, GroundLayerMask);
        }
    }
}
