using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PlacementSystem.Editor
{
    /// <summary>
    /// Visual improvements that cost nothing at runtime (everything is either
    /// baked in the editor or is just a different number in an existing shader).
    ///
    /// • Placement System ▸ Visuals ▸ Improve Scene — sun, fog, colour grading,
    ///   vignette, ground materials, reflection probe, baked environment lighting.
    /// • Placement System ▸ Visuals ▸ Improve Equipment Materials — metal /
    ///   paint / porcelain look for the equipment models.
    ///
    /// Both can be run again; values are simply set again.
    /// </summary>
    public static class VisualImprovements
    {
        private const string LightingSettingsPath = "Assets/Scenes/SampleSceneLighting.lighting";
        private const string EquipmentRoot = "Assets/PlacementSystem/Data/Categories";

        private static readonly string[] GroundMaterials =
        {
            "Assets/PlacementSystem/Materials/rocky_terrain_02.mat",
            "Assets/PlacementSystem/Materials/sandy_gravel.mat",
            "Assets/PlacementSystem/Materials/Terrain/Terrain.mat",
        };

        // ── Scene ─────────────────────────────────────────────────────────────

        [MenuItem("Placement System/Visuals/Improve Scene")]
        public static void ImproveScene()
        {
            var report = new List<string>();

            SetUpSun(report);
            SetUpFog(report);
            SetUpPostProcessing(report);
            FixGroundMaterials(report);
            SetUpReflectionProbe(report);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            // Baking needs the scene on disk.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Add("• Освещение окружения НЕ запечено: сцена не сохранена. Сохраните её и запустите команду ещё раз.");
                ShowReport(report);
                return;
            }

            BakeEnvironment(report);
            ShowReport(report);
        }

        private static void SetUpSun(List<string> report)
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (var light in Object.FindObjectsByType<Light>())
                {
                    if (light.type == LightType.Directional)
                    {
                        sun = light;
                        break;
                    }
                }
            }

            if (sun == null)
            {
                report.Add("• Солнце: направленный свет не найден — пропущено.");
                return;
            }

            Undo.RecordObject(sun, "Improve sun");
            Undo.RecordObject(sun.transform, "Improve sun");

            // Warm late-morning light instead of pure white
            sun.useColorTemperature = false;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 2.6f;
            sun.shadowStrength = 0.9f;

            // ~40° above the horizon: long, readable shadows; keep the current heading.
            var yaw = sun.transform.eulerAngles.y;
            sun.transform.rotation = Quaternion.Euler(40f, yaw, 0f);

            // The procedural sky draws its sun where this light points.
            RenderSettings.sun = sun;

            report.Add("• Солнце: тёплый цвет, 40° над горизонтом, привязано к небу.");
        }

        private static void SetUpFog(List<string> report)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0025f;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.85f);   // close to the sky horizon
            report.Add("• Туман: лёгкий, в цвет горизонта.");
        }

        private static void SetUpPostProcessing(List<string> report)
        {
            var volume = Object.FindAnyObjectByType<Volume>();
            var profile = volume != null ? volume.sharedProfile : null;
            if (profile == null)
            {
                report.Add("• Постобработка: Global Volume с профилем не найден — пропущено.");
                return;
            }

            Undo.RecordObject(profile, "Improve post-processing");

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.4f);

            // Colour grading is baked into one lookup table together with tonemapping,
            // which is already on — adding it costs nothing per frame.
            var colors = GetOrAdd<ColorAdjustments>(profile);
            colors.active = true;
            colors.contrast.Override(12f);
            colors.saturation.Override(-6f);
            colors.postExposure.Override(0.1f);

            var whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.active = true;
            whiteBalance.temperature.Override(6f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            report.Add("• Постобработка: виньетка слабее (0.16), контраст +12, насыщенность −6, чуть теплее баланс белого.");
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component))
                return component;

            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            // Components must be stored inside the profile asset, otherwise they are lost on reload.
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void FixGroundMaterials(List<string> report)
        {
            var fixedCount = 0;
            foreach (var path in GroundMaterials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;

                Undo.RecordObject(material, "Fix ground material");

                // Parallax (height map) is the most expensive effect in the scene.
                if (material.HasProperty("_ParallaxMap"))
                    material.SetTexture("_ParallaxMap", null);
                material.DisableKeyword("_PARALLAXMAP");

                // A roughness map was plugged into the metallic slot: URP reads metal from
                // red and smoothness from alpha there, so the ground came out shiny metal.
                if (material.HasProperty("_MetallicGlossMap"))
                {
                    var map = material.GetTexture("_MetallicGlossMap");
                    if (map != null && map.name.ToLowerInvariant().Contains("rough"))
                    {
                        material.SetTexture("_MetallicGlossMap", null);
                        material.DisableKeyword("_METALLICSPECGLOSSMAP");
                    }
                }
                if (material.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", 0f);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0.12f);

                // Same normal map used a second time as a detail map: one texture read for nothing.
                if (material.HasProperty("_DetailNormalMap") && material.HasProperty("_BumpMap") &&
                    material.GetTexture("_DetailNormalMap") == material.GetTexture("_BumpMap"))
                {
                    material.SetTexture("_DetailNormalMap", null);
                    material.DisableKeyword("_DETAIL_MULX2");
                    material.DisableKeyword("_DETAIL_SCALED");
                }

                EditorUtility.SetDirty(material);
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            report.Add($"• Земля ({fixedCount} материала): убран параллакс, исправлена «металлическая» карта, матовая поверхность.");
        }

        private static void SetUpReflectionProbe(List<string> report)
        {
            // Area of the environment (ground, fence…)
            var environment = GameObject.Find("Terrain");
            var bounds = new Bounds(Vector3.zero, new Vector3(100f, 20f, 100f));
            if (environment != null)
            {
                var renderers = environment.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    bounds = renderers[0].bounds;
                    foreach (var r in renderers)
                        bounds.Encapsulate(r.bounds);
                }

                // Only static objects are captured by a baked probe.
                foreach (var t in environment.GetComponentsInChildren<Transform>(true))
                {
                    var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags | StaticEditorFlags.ReflectionProbeStatic);
                }
            }

            var probe = Object.FindAnyObjectByType<ReflectionProbe>();
            if (probe == null)
            {
                var go = new GameObject("ReflectionProbe");
                Undo.RegisterCreatedObjectUndo(go, "Add reflection probe");
                probe = go.AddComponent<ReflectionProbe>();
            }

            Undo.RecordObject(probe, "Set up reflection probe");
            Undo.RecordObject(probe.transform, "Set up reflection probe");

            probe.transform.position = new Vector3(bounds.center.x, bounds.min.y + 4f, bounds.center.z);
            probe.mode = ReflectionProbeMode.Baked;          // baked once: no cost while running
            probe.resolution = 128;
            probe.hdr = false;
            probe.boxProjection = false;
            probe.size = new Vector3(Mathf.Max(bounds.size.x, 50f), Mathf.Max(bounds.size.y, 30f), Mathf.Max(bounds.size.z, 50f));
            probe.center = Vector3.zero;

            report.Add("• Reflection Probe (запекаемый) над площадкой: металл будет отражать окружение.");
        }

        private static void BakeEnvironment(List<string> report)
        {
            // Lighting settings without baked / realtime GI: only the sky ambient
            // light and reflections are generated. Nothing is computed at runtime.
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "SampleSceneLighting" };
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }

            settings.bakedGI = false;
            settings.realtimeGI = false;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Lightmapping.lightingSettings = settings;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            Lightmapping.BakeAsync();
            report.Add("• Освещение окружения и отражения запекаются (прогресс — в правом нижнем углу Unity). После окончания сохраните сцену.");
        }

        private static void ShowReport(List<string> report)
        {
            EditorUtility.DisplayDialog("Placement System — улучшение сцены", string.Join("\n", report), "OK");
            Debug.Log("[VisualImprovements]\n" + string.Join("\n", report));
        }

        // ── Equipment materials ───────────────────────────────────────────────

        /// <summary>
        /// The equipment models carry plain coloured materials inside the FBX files
        /// (read-only). They are copied to .mat files next to the prefabs, the
        /// prefabs are switched to the copies, and each copy gets metal / paint /
        /// porcelain settings guessed from its colour. The FBX files are not touched.
        /// </summary>
        [MenuItem("Placement System/Visuals/Improve Equipment Materials")]
        public static void ImproveEquipmentMaterials()
        {
            var copies = new Dictionary<Material, Material>();   // FBX material → editable copy
            var prefabCount = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EquipmentRoot }))
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                var changed = false;

                try
                {
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        // Connector markers are not part of the model.
                        if (renderer.GetComponentInParent<EnergyConnector>(true) != null)
                            continue;

                        var materials = renderer.sharedMaterials;
                        for (var i = 0; i < materials.Length; i++)
                        {
                            var source = materials[i];
                            if (source == null)
                                continue;

                            var sourcePath = AssetDatabase.GetAssetPath(source);
                            Material target;

                            if (sourcePath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                            {
                                if (!copies.TryGetValue(source, out target))
                                {
                                    target = CreateCopy(source, prefabPath);
                                    copies[source] = target;
                                }
                                materials[i] = target;
                                changed = true;
                            }
                            else if (sourcePath.EndsWith(".mat") && sourcePath.StartsWith(EquipmentRoot))
                            {
                                target = source;   // already a copy from an earlier run
                            }
                            else
                            {
                                continue;
                            }

                            ApplySurface(target);
                        }

                        renderer.sharedMaterials = materials;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        prefabCount++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();

            var message = $"Материалов скопировано: {copies.Count}, префабов обновлено: {prefabCount}.\n\n" +
                          "Серые — оцинкованная сталь, тёмные — резина/тёмная краска, светлые и бирюзовые — фарфор, " +
                          "цветные — краска. Копии лежат в папках Materials рядом с префабами; их можно подправить вручную.";
            EditorUtility.DisplayDialog("Placement System — материалы оборудования", message, "OK");
            Debug.Log("[VisualImprovements] " + message);
        }

        private static Material CreateCopy(Material source, string prefabPath)
        {
            var categoryFolder = Path.GetDirectoryName(Path.GetDirectoryName(prefabPath))?.Replace('\\', '/');
            var folder = categoryFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(categoryFolder, "Materials");

            var modelName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source));
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{modelName}_{source.name}.mat");

            var copy = new Material(source) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        /// <summary>Guesses the surface from the base colour of the imported material.</summary>
        private static void ApplySurface(Material material)
        {
            var c = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                  : material.HasProperty("_Color") ? material.GetColor("_Color")
                  : Color.gray;

            var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            var grey = max - min < 0.08f;

            float metallic, smoothness;
            if (grey && max < 0.18f)
            {
                metallic = 0f;      smoothness = 0.25f;   // rubber, dark paint
            }
            else if (grey && max > 0.9f)
            {
                metallic = 0f;      smoothness = 0.75f;   // white porcelain
            }
            else if (grey)
            {
                metallic = 0.75f;   smoothness = 0.45f;   // galvanized steel
            }
            else if (c.g > 0.9f && c.b > 0.9f)
            {
                metallic = 0f;      smoothness = 0.8f;    // light glazed porcelain
            }
            else
            {
                metallic = 0.1f;    smoothness = 0.5f;    // paint: tanks, phase colours, polymer insulators
            }

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);

            EditorUtility.SetDirty(material);
        }
    }
}
