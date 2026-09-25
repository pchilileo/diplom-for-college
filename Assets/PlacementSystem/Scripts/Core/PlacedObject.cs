using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlacementSystem
{
    [DisallowMultipleComponent]
    public class PlacedObject : MonoBehaviour
    {
        [SerializeField] private string objectId;
        [SerializeField] private AssetData sourceAsset;

        // Highlight tint applied when object is selected
        [SerializeField] private Color selectionTint = new Color(0.4f, 0.8f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float selectionTintStrength = 0.45f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        private Renderer[] cachedRenderers;
        private Color[][] originalColors;   // [rendererIndex][materialIndex]
        private MaterialPropertyBlock propertyBlock;
        private bool isSelected;

        public string ObjectId => objectId;
        public AssetData SourceAsset => sourceAsset;

        public event Action<PlacedObject> TransformChanged;

        /// <summary>
        /// Fired when a wire connection is added or removed on any of this object's connectors.
        /// Subscribe to keep external systems (UI, save data, etc.) in sync.
        /// </summary>
        public event Action<PlacedObject> ConnectionsChanged;

        // Cached list of EnergyConnector children — populated lazily on first access.
        private List<EnergyConnector> cachedConnectors;

        /// <summary>All EnergyConnector points that belong to this object.</summary>
        public IReadOnlyList<EnergyConnector> Connectors
        {
            get
            {
                if (cachedConnectors == null)
                    RebuildConnectorCache();
                return cachedConnectors;
            }
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureColliders();
            CacheRenderers();
        }

        private void OnDestroy()
        {
            TransformChanged   = null;
            ConnectionsChanged = null;
            // EnergyConnector.OnDestroy handles wire cleanup for each connector.
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Initialize(AssetData asset, string id)
        {
            sourceAsset = asset;
            objectId = id;
            name = asset != null ? asset.DisplayName : name;

            // Re-cache after prefab is fully initialised
            CacheRenderers();
            RebuildConnectorCache();
        }

        public void NotifyTransformChanged()
        {
            TransformChanged?.Invoke(this);
        }

        /// <summary>
        /// Called by <see cref="WireConnectionMode"/> whenever a wire is added or
        /// removed. Fires <see cref="ConnectionsChanged"/> so listeners can react.
        /// </summary>
        public void NotifyConnectionsChanged()
        {
            ConnectionsChanged?.Invoke(this);
        }

        /// <summary>
        /// Forces a rebuild of the connector cache. Call this if connectors are
        /// added or removed from the hierarchy at runtime.
        /// </summary>
        public void RebuildConnectorCache()
        {
            cachedConnectors ??= new List<EnergyConnector>();
            cachedConnectors.Clear();
            GetComponentsInChildren(true, cachedConnectors);
        }

        /// <summary>
        /// Returns the world-space Y offset needed so the object sits on top of
        /// the ground plane instead of half-way through it.
        /// </summary>
        public float GetGroundOffset()
        {
            var bounds = GetWorldBounds();
            // Distance from pivot to the bottom of the combined bounds
            return transform.position.y - bounds.min.y;
        }

        /// <summary>Returns the combined world-space bounds of the model renderers.</summary>
        public Bounds GetWorldBounds()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
                return new Bounds(transform.position, Vector3.one);

            var bounds = cachedRenderers[0].bounds;
            for (var i = 1; i < cachedRenderers.Length; i++)
                bounds.Encapsulate(cachedRenderers[i].bounds);

            return bounds;
        }

        // ── Colliders ─────────────────────────────────────────────────────────

        /// <summary>
        /// Gives every model mesh in the hierarchy its own collider so a click
        /// anywhere on the model selects it.
        ///
        /// A MeshCollider created at runtime needs a mesh with Read/Write enabled —
        /// the editor ignores this, but in a build such a collider silently gets no
        /// geometry. Non-readable meshes therefore fall back to a BoxCollider,
        /// which only needs the mesh bounds.
        /// </summary>
        private void EnsureColliders()
        {
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (IsServiceObject(filter.transform))
                    continue;

                if (filter.GetComponent<Collider>() != null)
                    continue;

                var mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;

                if (mesh.isReadable)
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
                else
                    filter.gameObject.AddComponent<BoxCollider>();
            }

            if (GetComponentInChildren<Collider>() == null)
                gameObject.AddComponent<BoxCollider>();
        }

        /// <summary>Connector points and wires are not part of the model itself.</summary>
        private static bool IsServiceObject(Transform t)
        {
            return t.GetComponentInParent<EnergyConnector>(true) != null
                || t.GetComponentInParent<WireConnection>(true) != null;
        }

        // ── Selection highlight ───────────────────────────────────────────────

        public void SetSelected(bool selected)
        {
            if (isSelected == selected)
                return;

            isSelected = selected;
            ApplyHighlight(selected);
        }

        /// <summary>
        /// Tints the model through a MaterialPropertyBlock: materials are not
        /// duplicated and nothing has to be restored on the assets afterwards.
        /// </summary>
        private void ApplyHighlight(bool selected)
        {
            if (cachedRenderers == null)
                return;

            propertyBlock ??= new MaterialPropertyBlock();

            for (var r = 0; r < cachedRenderers.Length; r++)
            {
                var renderer = cachedRenderers[r];
                if (renderer == null)
                    continue;

                for (var m = 0; m < originalColors[r].Length; m++)
                {
                    propertyBlock.Clear();

                    if (selected)
                    {
                        var tinted = Color.Lerp(originalColors[r][m], selectionTint, selectionTintStrength);
                        propertyBlock.SetColor(BaseColorId, tinted);   // URP Lit / Unlit
                        propertyBlock.SetColor(ColorId, tinted);       // Built-in / legacy
                    }

                    renderer.SetPropertyBlock(propertyBlock, m);
                }
            }
        }

        private void CacheRenderers()
        {
            var renderers = new List<Renderer>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!IsServiceObject(renderer.transform))
                    renderers.Add(renderer);
            }

            cachedRenderers = renderers.ToArray();
            originalColors = new Color[cachedRenderers.Length][];

            for (var r = 0; r < cachedRenderers.Length; r++)
            {
                var mats = cachedRenderers[r].sharedMaterials;
                originalColors[r] = new Color[mats.Length];
                for (var m = 0; m < mats.Length; m++)
                    originalColors[r][m] = GetMaterialColor(mats[m]);
            }
        }

        private static Color GetMaterialColor(Material mat)
        {
            if (mat == null)
                return Color.white;
            if (mat.HasProperty(BaseColorId))
                return mat.GetColor(BaseColorId);
            if (mat.HasProperty(ColorId))
                return mat.GetColor(ColorId);
            return Color.white;
        }
    }
}
