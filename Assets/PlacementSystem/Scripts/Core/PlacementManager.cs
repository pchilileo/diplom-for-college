using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlacementSystem
{
    public class PlacementManager : MonoBehaviour
    {
        public static PlacementManager Instance { get; private set; }

        [SerializeField] private GridSnapSettings snapSettings = GridSnapSettings.Default;
        [SerializeField] private Material previewMaterial;

        private readonly Dictionary<string, PlacedObject> placedObjects = new();
        private int nextId = 1;
        private GameObject previewInstance;

        public GridSnapSettings SnapSettings => snapSettings;
        public Material PreviewMaterial => previewMaterial;

        public event Action<PlacedObject> ObjectSpawned;
        public event Action<PlacedObject> ObjectRemoved;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        // ── Spawning ───────────────────────────────────────────────────────────

        public PlacedObject Spawn(AssetData data, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (data == null || data.Prefab == null)
            {
                Debug.LogWarning("PlacementManager: Cannot spawn — AssetData or Prefab is null.");
                return null;
            }

            position = snapSettings.SnapPosition(position);
            var euler = snapSettings.SnapRotation(rotation.eulerAngles);
            rotation = Quaternion.Euler(euler);

            var instance = Instantiate(data.Prefab, position, rotation, transform);
            instance.transform.localScale = scale;

            var placed = instance.GetComponent<PlacedObject>();
            if (placed == null)
                placed = instance.AddComponent<PlacedObject>();

            var id = GenerateId();
            placed.Initialize(data, id);
            placedObjects[id] = placed;

            // FIX: lift the object so its bottom sits on the ground plane
            LiftToGround(placed);

            EnsureCollider(instance);
            ObjectSpawned?.Invoke(placed);
            return placed;
        }

        public PlacedObject Spawn(AssetData data, Vector3 position)
        {
            return Spawn(data, position, Quaternion.identity, Vector3.one);
        }

        // ── Preview ────────────────────────────────────────────────────────────

        public GameObject CreatePreview(AssetData data)
        {
            DestroyPreview();

            if (data == null || data.Prefab == null)
                return null;

            previewInstance = Instantiate(data.Prefab);
            previewInstance.name = $"Preview_{data.DisplayName}";
            SetPreviewAppearance(previewInstance);
            DisablePreviewPhysics(previewInstance);
            return previewInstance;
        }

        public void UpdatePreviewPosition(Vector3 position)
        {
            if (previewInstance == null)
                return;

            position = snapSettings.SnapPosition(position);

            // FIX: offset preview so its bottom sits on the surface, not its pivot
            var offset = GetPivotToBottomOffset(previewInstance);
            previewInstance.transform.position = position + Vector3.up * offset;
        }

        public PlacedObject CommitPreview(AssetData data, Vector3 position)
        {
            var rotation = previewInstance != null ? previewInstance.transform.rotation : Quaternion.identity;
            var scale    = previewInstance != null ? previewInstance.transform.localScale : Vector3.one;
            DestroyPreview();
            // Spawn handles the ground-lift itself
            return Spawn(data, position, rotation, scale);
        }

        public void DestroyPreview()
        {
            if (previewInstance == null)
                return;

            Destroy(previewInstance);
            previewInstance = null;
        }

        // ── Remove ─────────────────────────────────────────────────────────────

        public void Remove(PlacedObject placedObject)
        {
            if (placedObject == null)
                return;

            placedObjects.Remove(placedObject.ObjectId);
            ObjectRemoved?.Invoke(placedObject);
            Destroy(placedObject.gameObject);
        }

        public bool TryGetById(string id, out PlacedObject placedObject)
        {
            return placedObjects.TryGetValue(id, out placedObject);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Moves <paramref name="placed"/> upward so its lowest renderer bound
        /// sits exactly on the Y of its spawn position.
        /// </summary>
        private static void LiftToGround(PlacedObject placed)
        {
            var offset = placed.GetGroundOffset();
            if (offset > 0.0001f)
                placed.transform.position += Vector3.up * offset;
        }

        /// <summary>
        /// Returns how far above the given GameObject's pivot its bottom bound is.
        /// Used to raise the preview so it doesn't sink into the ground.
        /// </summary>
        private static float GetPivotToBottomOffset(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 0f;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            // pivot Y minus bottom Y  →  how much to lift
            return root.transform.position.y - bounds.min.y;
        }

        private string GenerateId()
        {
            return $"placed_{nextId++}_{Guid.NewGuid().ToString("N")[..8]}";
        }

        private void SetPreviewAppearance(GameObject root)
        {
            if (previewMaterial == null)
                return;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[renderer.materials.Length];
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = previewMaterial;

                renderer.materials = materials;
            }
        }

        private static void DisablePreviewPhysics(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>())
                collider.enabled = false;

            foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;
        }

        private static void EnsureCollider(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>() != null)
                return;

            var filter = root.GetComponentInChildren<MeshFilter>();
            if (filter != null)
            {
                var meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.convex = true;
                return;
            }

            root.AddComponent<BoxCollider>();
        }
    }
}