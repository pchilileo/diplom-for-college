using System.Collections.Generic;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Attach this component to a child GameObject (tag: "EnergyConnector") of any
    /// PlacedObject prefab to mark it as a wire connection point.
    ///
    /// ── Setup in prefab ───────────────────────────────────────────────────────
    /// 1. Inside your prefab hierarchy add an empty child GameObject.
    /// 2. Set its Tag to "EnergyConnector".
    /// 3. Add this component to it.
    /// 4. Position it where the wire should visually attach (e.g. a socket/port).
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public class EnergyConnector : MonoBehaviour
    {
        // All wires currently attached to this connector
        private readonly List<WireConnection> connections = new();

        /// <summary>The PlacedObject that owns this connector.</summary>
        public PlacedObject Owner { get; private set; }

        /// <summary>Read-only view of active connections.</summary>
        public IReadOnlyList<WireConnection> Connections => connections;

        // ── Highlight state ───────────────────────────────────────────────────

        private Renderer[] visualRenderers;

        private static readonly Color IdleColor     = new(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color AvailableColor = new(0.2f, 0.8f, 1.0f, 1f);   // cyan  – mode active
        private static readonly Color SelectedColor  = new(0.1f, 0.9f, 0.2f, 1f);   // green – first pick
        private static readonly Color HoverColor     = new(1.0f, 0.85f, 0.1f, 1f);  // yellow – hover

        public enum HighlightState { Idle, Available, Selected, Hover }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Walk up to find the owning PlacedObject
            Owner = GetComponentInParent<PlacedObject>();

            // Cache any renderers on this connector visual (optional sphere/mesh)
            visualRenderers = GetComponentsInChildren<Renderer>();

            // Connectors are hidden by default; WireConnectionMode shows them
            // only while wire-connection mode is active.
            GetComponent<MeshRenderer>().enabled = false;
        }

        private void OnDestroy()
        {
            // Destroy all wires attached to this connector so nothing is left dangling
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                if (connections[i] != null)
                    connections[i].DestroyWire();
            }
            connections.Clear();
        }

        // ── Connection management ─────────────────────────────────────────────

        public void RegisterConnection(WireConnection wire)
        {
            if (!connections.Contains(wire))
                connections.Add(wire);
        }

        public void UnregisterConnection(WireConnection wire)
        {
            connections.Remove(wire);
        }

        // ── Visual highlight ──────────────────────────────────────────────────

        public void SetHighlight(HighlightState state)
        {
            var color = state switch
            {
                HighlightState.Available => AvailableColor,
                HighlightState.Selected  => SelectedColor,
                HighlightState.Hover     => HoverColor,
                _                        => IdleColor,
            };

            foreach (var r in visualRenderers)
            {
                // Works with Standard, URP Lit and Unlit shaders
                foreach (var mat in r.materials)
                {
                    if (mat.HasProperty("_Color"))
                        mat.color = color;
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        if (state != HighlightState.Idle)
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.SetColor("_EmissionColor", color * 0.6f);
                        }
                        else
                        {
                            mat.SetColor("_EmissionColor", Color.black);
                            mat.DisableKeyword("_EMISSION");
                        }
                    }
                }
            }
        }
    }
}