using UnityEngine;

namespace PlacementSystem
{
    [System.Serializable]
    public struct GridSnapSettings
    {
        public bool enablePositionSnap;
        public float positionStep;
        public bool enableRotationSnap;
        public float rotationStep;

        public static GridSnapSettings Default => new()
        {
            enablePositionSnap = true,
            positionStep = 0.5f,
            enableRotationSnap = true,
            rotationStep = 15f
        };

        public Vector3 SnapPosition(Vector3 position)
        {
            if (!enablePositionSnap || positionStep <= 0f)
                return position;

            return new Vector3(
                SnapAxis(position.x, positionStep),
                SnapAxis(position.y, positionStep),
                SnapAxis(position.z, positionStep));
        }

        public Vector3 SnapRotation(Vector3 euler)
        {
            if (!enableRotationSnap || rotationStep <= 0f)
                return euler;

            return new Vector3(
                SnapAxis(euler.x, rotationStep),
                SnapAxis(euler.y, rotationStep),
                SnapAxis(euler.z, rotationStep));
        }

        private static float SnapAxis(float value, float step)
        {
            return Mathf.Round(value / step) * step;
        }
    }
}
