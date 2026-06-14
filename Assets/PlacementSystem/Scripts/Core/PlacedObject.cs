using System;
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
        [SerializeField] private float selectionEmissionIntensity = 0.35f;

        private Renderer[] cachedRenderers;
        private Color[][] originalColors;   // [rendererIndex][materialIndex]
        private bool isSelected;

        public string ObjectId => objectId;
        public AssetData SourceAsset => sourceAsset;

        public event Action<PlacedObject> TransformChanged;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnDestroy()
        {
            TransformChanged = null;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Initialize(AssetData asset, string id)
        {
            sourceAsset = asset;
            objectId = id;
            name = asset != null ? asset.DisplayName : name;

            // Re-cache after prefab is fully initialised
            CacheRenderers();
        }

        public void NotifyTransformChanged()
        {
            TransformChanged?.Invoke(this);
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

        /// <summary>Returns the combined world-space bounds of all renderers.</summary>
        public Bounds GetWorldBounds()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
                return new Bounds(transform.position, Vector3.one);

            var bounds = cachedRenderers[0].bounds;
            for (var i = 1; i < cachedRenderers.Length; i++)
                bounds.Encapsulate(cachedRenderers[i].bounds);

            return bounds;
        }

        // ── Selection highlight ───────────────────────────────────────────────

        public void SetSelected(bool selected)
        {
            if (isSelected == selected)
                return;

            isSelected = selected;

            if (selected)
                ApplyHighlight();
            else
                RemoveHighlight();
        }

        private void ApplyHighlight()
        {
            if (cachedRenderers == null)
                return;

            for (var r = 0; r < cachedRenderers.Length; r++)
            {
                var mats = cachedRenderers[r].materials;
                for (var m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];

                    // Additive colour tint
                    if (mat.HasProperty("_Color"))
                        mat.color = Color.Lerp(originalColors[r][m], selectionTint, 0.35f);

                    // Emission glow (Standard / URP Lit shaders)
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", selectionTint * selectionEmissionIntensity);
                    }
                }
                cachedRenderers[r].materials = mats;
            }
        }

        private void RemoveHighlight()
        {
            if (cachedRenderers == null)
                return;

            for (var r = 0; r < cachedRenderers.Length; r++)
            {
                var mats = cachedRenderers[r].materials;
                for (var m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];

                    if (mat.HasProperty("_Color"))
                        mat.color = originalColors[r][m];

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", Color.black);
                        mat.DisableKeyword("_EMISSION");
                    }
                }
                cachedRenderers[r].materials = mats;
            }
        }

        private void CacheRenderers()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[cachedRenderers.Length][];

            for (var r = 0; r < cachedRenderers.Length; r++)
            {
                var mats = cachedRenderers[r].materials;
                originalColors[r] = new Color[mats.Length];
                for (var m = 0; m < mats.Length; m++)
                    originalColors[r][m] = mats[m].HasProperty("_Color")
                        ? mats[m].color
                        : Color.white;
            }
        }
    }
}