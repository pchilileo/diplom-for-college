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
        private MaterialPropertyBlock propertyBlock;

        private static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId         = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

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
            visualRenderers = GetComponentsInChildren<Renderer>(true);

            // Connectors are hidden by default; WireConnectionMode shows them
            // only while wire-connection mode is active.
            SetVisible(false);
        }

        private void Start()
        {
            // PlacedObject is added to the spawned instance after Instantiate,
            // i.e. after this Awake has already run.
            if (Owner == null)
                Owner = GetComponentInParent<PlacedObject>();
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

        /// <summary>True if a wire already joins this connector with <paramref name="other"/>.</summary>
        public bool IsConnectedTo(EnergyConnector other)
        {
            foreach (var wire in connections)
            {
                if (wire == null)
                    continue;
                if ((wire.ConnectorA == this && wire.ConnectorB == other) ||
                    (wire.ConnectorB == this && wire.ConnectorA == other))
                    return true;
            }
            return false;
        }

        // ── Visual highlight ──────────────────────────────────────────────────

        /// <summary>Shows or hides the connector marker. The collider is not touched.</summary>
        public void SetVisible(bool visible)
        {
            foreach (var r in visualRenderers)
            {
                if (r != null)
                    r.enabled = visible;
            }
        }

        public void SetHighlight(HighlightState state)
        {
            var color = state switch
            {
                HighlightState.Available => AvailableColor,
                HighlightState.Selected  => SelectedColor,
                HighlightState.Hover     => HoverColor,
                _                        => IdleColor,
            };

            // Emission only shows up if the marker material already has it enabled;
            // keywords can't be switched through a property block.
            var emission = state != HighlightState.Idle ? color * 0.6f : Color.black;

            propertyBlock ??= new MaterialPropertyBlock();

            foreach (var r in visualRenderers)
            {
                if (r == null)
                    continue;

                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);       // URP Lit / Unlit
                propertyBlock.SetColor(ColorId, color);           // Built-in / legacy
                propertyBlock.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
