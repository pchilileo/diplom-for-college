using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>A wire of the scene expressed as object / connector indices.</summary>
    public readonly struct SnapshotWire
    {
        public readonly WireConnection Wire;
        public readonly int ObjectA, ConnectorA, ObjectB, ConnectorB;

        public SnapshotWire(WireConnection wire, int objectA, int connectorA, int objectB, int connectorB)
        {
            Wire = wire;
            ObjectA = objectA;
            ConnectorA = connectorA;
            ObjectB = objectB;
            ConnectorB = connectorB;
        }
    }

    /// <summary>
    /// Placed equipment and wires of the current scene. Connectors are identified
    /// by their index in <see cref="PlacedObject.Connectors"/>, which is the same
    /// for every instance of a prefab.
    /// </summary>
    public sealed class SceneSnapshot
    {
        public readonly List<PlacedObject> Objects = new();
        public readonly List<Vector3> RelativePositions = new();
        public readonly List<SnapshotWire> Wires = new();

        public static SceneSnapshot Capture()
        {
            var snapshot = new SceneSnapshot();

            snapshot.Objects.AddRange(UnityEngine.Object.FindObjectsByType<PlacedObject>()
                .Where(o => o.SourceAsset != null)
                .OrderBy(o => o.ObjectId, StringComparer.Ordinal));

            var index = new Dictionary<PlacedObject, int>();
            var centre = Vector3.zero;
            for (var i = 0; i < snapshot.Objects.Count; i++)
            {
                index[snapshot.Objects[i]] = i;
                centre += snapshot.Objects[i].PivotPoint;
            }
            if (snapshot.Objects.Count > 0)
                centre /= snapshot.Objects.Count;

            foreach (var o in snapshot.Objects)
                snapshot.RelativePositions.Add(o.PivotPoint - centre);

            foreach (var wire in UnityEngine.Object.FindObjectsByType<WireConnection>())
            {
                if (!TryLocate(wire.ConnectorA, index, out var a, out var ca) ||
                    !TryLocate(wire.ConnectorB, index, out var b, out var cb))
                    continue;

                snapshot.Wires.Add(new SnapshotWire(wire, a, ca, b, cb));
            }

            return snapshot;
        }

        public SubstationSchema ToSchema()
        {
            var schema = new SubstationSchema { createdUtc = DateTime.UtcNow.ToString("o") };

            for (var i = 0; i < Objects.Count; i++)
            {
                var asset = Objects[i].SourceAsset;
                schema.objects.Add(new SchemaObject
                {
                    asset = asset.name,
                    displayName = asset.DisplayName,
                    position = RelativePositions[i],
                });
            }

            foreach (var w in Wires)
            {
                schema.wires.Add(new SchemaWire
                {
                    objectA = w.ObjectA, connectorA = w.ConnectorA,
                    objectB = w.ObjectB, connectorB = w.ConnectorB,
                });
            }

            return schema;
        }

        private static bool TryLocate(EnergyConnector connector, Dictionary<PlacedObject, int> index,
            out int objectIndex, out int connectorIndex)
        {
            objectIndex = connectorIndex = -1;
            if (connector == null)
                return false;

            var owner = connector.Owner != null ? connector.Owner : connector.GetComponentInParent<PlacedObject>();
            if (owner == null || !index.TryGetValue(owner, out objectIndex))
                return false;

            var connectors = owner.Connectors;
            for (var i = 0; i < connectors.Count; i++)
            {
                if (connectors[i] == connector)
                {
                    connectorIndex = i;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Outcome of comparing the scene with a reference schema.</summary>
    public sealed class CheckResult
    {
        public string FileName;
        public string SchemaId;
        public string ModeName;
        public int ReferenceWires;

        public readonly List<WireConnection> CorrectWires = new();
        public readonly List<WireConnection> WrongWires = new();
        /// <summary>Reference wires that are absent in the scene, as scene connector pairs (drawn as ghosts).</summary>
        public readonly List<(EnergyConnector a, EnergyConnector b)> MissingWires = new();
        /// <summary>Missing reference wires that can't be shown because their equipment is not in the scene.</summary>
        public int MissingWithoutObjects;

        /// <summary>"Name ×count" of equipment that is in the reference but not in the scene.</summary>
        public readonly List<string> MissingObjects = new();
        /// <summary>"Name ×count" of equipment in the scene that the reference does not have.</summary>
        public readonly List<string> ExtraObjects = new();

        public int MissingTotal => MissingWires.Count + MissingWithoutObjects;
        public bool IsPerfect => WrongWires.Count == 0 && MissingTotal == 0 && MissingObjects.Count == 0;
    }

    /// <summary>
    /// Compares the scene wiring with a reference schema.
    ///
    /// Objects of the two layouts are paired by equipment type. When several
    /// objects share a type, the pairing that makes the most reference wires
    /// match is chosen (hill climbing over swaps), starting from the pairing by
    /// position within the layout — so identical equipment may be placed anywhere,
    /// and ties are resolved by where it stands.
    /// </summary>
    public static class SchemaMatcher
    {
        public static CheckResult Compare(SubstationSchema reference, SceneSnapshot scene)
        {
            var refCount = reference.objects.Count;
            var sceneCount = scene.Objects.Count;

            var refToScene = Enumerable.Repeat(-1, refCount).ToArray();
            var sceneToRef = Enumerable.Repeat(-1, sceneCount).ToArray();

            var refByType = GroupBy(refCount, i => reference.objects[i].asset);
            var sceneByType = GroupBy(sceneCount, i => scene.Objects[i].SourceAsset.name);

            // Scene wires as a set of connector pairs
            var sceneWireSet = new HashSet<(int, int, int, int)>();
            foreach (var w in scene.Wires)
                sceneWireSet.Add(Key(w.ObjectA, w.ConnectorA, w.ObjectB, w.ConnectorB));

            InitialPairing(reference, scene, refByType, sceneByType, refToScene, sceneToRef);
            ImprovePairing(reference, refByType, sceneByType, sceneWireSet, refToScene, sceneToRef);

            return BuildResult(reference, scene, refToScene, sceneToRef, refByType, sceneByType);
        }

        // ── Pairing ───────────────────────────────────────────────────────────

        /// <summary>Greedy: closest (relative) positions first, per equipment type.</summary>
        private static void InitialPairing(SubstationSchema reference, SceneSnapshot scene,
            Dictionary<string, List<int>> refByType, Dictionary<string, List<int>> sceneByType,
            int[] refToScene, int[] sceneToRef)
        {
            foreach (var (type, refs) in refByType)
            {
                if (!sceneByType.TryGetValue(type, out var candidates))
                    continue;

                var pairs = new List<(float distance, int r, int s)>();
                foreach (var r in refs)
                    foreach (var s in candidates)
                        pairs.Add(((reference.objects[r].position - scene.RelativePositions[s]).sqrMagnitude, r, s));

                foreach (var (_, r, s) in pairs.OrderBy(p => p.distance))
                {
                    if (refToScene[r] >= 0 || sceneToRef[s] >= 0)
                        continue;
                    refToScene[r] = s;
                    sceneToRef[s] = r;
                }
            }
        }

        /// <summary>Swaps objects of the same type while that increases the number of matching wires.</summary>
        private static void ImprovePairing(SubstationSchema reference,
            Dictionary<string, List<int>> refByType, Dictionary<string, List<int>> sceneByType,
            HashSet<(int, int, int, int)> sceneWireSet, int[] refToScene, int[] sceneToRef)
        {
            var best = Score(reference, sceneWireSet, refToScene);

            for (var pass = 0; pass < 50; pass++)
            {
                var improved = false;

                foreach (var (type, refs) in refByType)
                {
                    if (!sceneByType.TryGetValue(type, out var candidates) || (refs.Count < 2 && candidates.Count < 2))
                        continue;

                    foreach (var r in refs)
                    {
                        foreach (var s in candidates)
                        {
                            if (refToScene[r] == s)
                                continue;

                            // Give scene object s to r; whoever had s gets r's old object.
                            var oldS = refToScene[r];
                            var otherR = sceneToRef[s];
                            Assign(r, s, otherR, oldS, refToScene, sceneToRef);

                            var score = Score(reference, sceneWireSet, refToScene);
                            if (score > best)
                            {
                                best = score;
                                improved = true;
                            }
                            else
                            {
                                // Undo
                                Assign(r, oldS, otherR, s, refToScene, sceneToRef);
                            }
                        }
                    }
                }

                if (!improved)
                    break;
            }
        }

        /// <summary>r ↦ s and otherR ↦ otherS (either index may be -1 = unpaired).</summary>
        private static void Assign(int r, int s, int otherR, int otherS, int[] refToScene, int[] sceneToRef)
        {
            refToScene[r] = s;
            if (s >= 0) sceneToRef[s] = r;

            if (otherR >= 0)
            {
                refToScene[otherR] = otherS;
                if (otherS >= 0) sceneToRef[otherS] = otherR;
            }
            else if (otherS >= 0)
            {
                sceneToRef[otherS] = -1;
            }
        }

        private static int Score(SubstationSchema reference, HashSet<(int, int, int, int)> sceneWireSet, int[] refToScene)
        {
            var score = 0;
            foreach (var w in reference.wires)
            {
                if (!InRange(w, refToScene.Length))
                    continue;

                var a = refToScene[w.objectA];
                var b = refToScene[w.objectB];
                if (a >= 0 && b >= 0 && sceneWireSet.Contains(Key(a, w.connectorA, b, w.connectorB)))
                    score++;
            }
            return score;
        }

        // ── Result ────────────────────────────────────────────────────────────

        private static CheckResult BuildResult(SubstationSchema reference, SceneSnapshot scene,
            int[] refToScene, int[] sceneToRef,
            Dictionary<string, List<int>> refByType, Dictionary<string, List<int>> sceneByType)
        {
            var result = new CheckResult { ReferenceWires = reference.wires.Count };

            // Reference wires in scene numbering
            var expected = new HashSet<(int, int, int, int)>();
            foreach (var w in reference.wires)
            {
                if (!InRange(w, refToScene.Length))
                {
                    result.MissingWithoutObjects++;
                    continue;
                }

                var a = refToScene[w.objectA];
                var b = refToScene[w.objectB];
                if (a < 0 || b < 0)
                {
                    result.MissingWithoutObjects++;
                    continue;
                }

                expected.Add(Key(a, w.connectorA, b, w.connectorB));
            }

            // Scene wires: correct if they are in the reference (each reference wire counts once)
            var found = new HashSet<(int, int, int, int)>();
            foreach (var w in scene.Wires)
            {
                var key = Key(w.ObjectA, w.ConnectorA, w.ObjectB, w.ConnectorB);
                if (expected.Contains(key) && found.Add(key))
                    result.CorrectWires.Add(w.Wire);
                else
                    result.WrongWires.Add(w.Wire);
            }

            // Reference wires nobody made: show them between the paired scene connectors
            foreach (var key in expected)
            {
                if (found.Contains(key))
                    continue;

                var (a, ca, b, cb) = key;
                var connectorsA = scene.Objects[a].Connectors;
                var connectorsB = scene.Objects[b].Connectors;
                if (ca < connectorsA.Count && cb < connectorsB.Count)
                    result.MissingWires.Add((connectorsA[ca], connectorsB[cb]));
                else
                    result.MissingWithoutObjects++;
            }

            // Equipment present on only one side
            foreach (var (_, refs) in refByType)
            {
                var missing = refs.Where(r => refToScene[r] < 0).ToList();
                if (missing.Count > 0)
                    result.MissingObjects.Add(Counted(reference.objects[missing[0]].displayName, missing.Count));
            }
            foreach (var (_, objects) in sceneByType)
            {
                var extra = objects.Where(s => sceneToRef[s] < 0).ToList();
                if (extra.Count > 0)
                    result.ExtraObjects.Add(Counted(scene.Objects[extra[0]].SourceAsset.DisplayName, extra.Count));
            }

            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Order-independent key of a wire between two connectors.</summary>
        private static (int, int, int, int) Key(int a, int ca, int b, int cb)
        {
            return a < b || (a == b && ca <= cb) ? (a, ca, b, cb) : (b, cb, a, ca);
        }

        private static bool InRange(SchemaWire w, int objectCount)
        {
            return w.objectA >= 0 && w.objectA < objectCount && w.objectB >= 0 && w.objectB < objectCount;
        }

        private static Dictionary<string, List<int>> GroupBy(int count, Func<int, string> type)
        {
            var groups = new Dictionary<string, List<int>>();
            for (var i = 0; i < count; i++)
            {
                var key = type(i) ?? string.Empty;
                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<int>();
                list.Add(i);
            }
            return groups;
        }

        private static string Counted(string name, int count)
        {
            return count > 1 ? $"{name} ×{count}" : name;
        }
    }
}
