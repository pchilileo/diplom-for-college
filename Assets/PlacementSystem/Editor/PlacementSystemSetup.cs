using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PlacementSystem.Editor
{
    public static class PlacementSystemSetup
    {
        private const string RootFolder = "Assets/PlacementSystem";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string DataFolder = RootFolder + "/Data";
        private const string MaterialFolder = RootFolder + "/Materials";

        [MenuItem("Placement System/Setup Scene")]
        public static void SetupScene()
        {
            EnsureFolders();
            EnsureGroundLayer();

            var previewMaterial = CreatePreviewMaterial();
            var samplePrefabs = CreateSamplePrefabs();
            var database = CreateSampleDatabase(samplePrefabs);
            var uiPrefabs = CreateUiPrefabs();

            ConfigureGroundPlane();
            ConfigureCamera();
            var managers = CreateManagers(previewMaterial);
            var canvas = CreateUi(database, uiPrefabs, managers);

            WireManagers(managers, canvas, previewMaterial);

            EditorUtility.DisplayDialog(
                "Placement System",
                "Сцена настроена.\n\n" +
                "Управление:\n" +
                "WASD — движение камеры\n" +
                "Q/E или колёсико — вверх/вниз\n" +
                "ПКМ — обзор\n" +
                "Shift — ускорение\n" +
                "Drag из левой панели — размещение\n" +
                "ЛКМ по объекту — выделение\n" +
                "Delete — удаление",
                "OK");

            Debug.Log("Placement System: setup complete.");
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
        }

        private static void EnsureGroundLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var groundLayerIndex = 8;

            var property = layers.GetArrayElementAtIndex(groundLayerIndex);
            if (string.IsNullOrEmpty(property.stringValue))
            {
                property.stringValue = "Ground";
                tagManager.ApplyModifiedProperties();
            }
        }

        private static Material CreatePreviewMaterial()
        {
            var path = MaterialFolder + "/PreviewGhost.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader)
            {
                color = new Color(0.3f, 0.7f, 1f, 0.45f)
            };

            if (shader.name.Contains("Universal"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject[] CreateSamplePrefabs()
        {
            var names = new[] { "Chair", "Table", "Lamp", "TV", "Plant", "Vase" };
            var primitives = new[]
            {
                PrimitiveType.Cylinder,
                PrimitiveType.Cube,
                PrimitiveType.Sphere,
                PrimitiveType.Cube,
                PrimitiveType.Cylinder,
                PrimitiveType.Sphere
            };

            var result = new GameObject[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var path = $"{PrefabFolder}/{names[i]}.prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null)
                {
                    result[i] = existing;
                    continue;
                }

                var go = GameObject.CreatePrimitive(primitives[i]);
                go.name = names[i];
                go.transform.localScale = i switch
                {
                    0 => new Vector3(0.5f, 0.5f, 0.5f),
                    1 => new Vector3(1.2f, 0.1f, 0.8f),
                    2 => new Vector3(0.3f, 0.3f, 0.3f),
                    3 => new Vector3(1.4f, 0.8f, 0.08f),
                    4 => new Vector3(0.25f, 0.6f, 0.25f),
                    _ => new Vector3(0.35f, 0.35f, 0.35f)
                };

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
                result[i] = prefab;
            }

            return result;
        }

        private static PlacementAssetDatabase CreateSampleDatabase(GameObject[] prefabs)
        {
            var dbPath = DataFolder + "/PlacementAssetDatabase.asset";
            var database = AssetDatabase.LoadAssetAtPath<PlacementAssetDatabase>(dbPath);
            if (database == null)
                database = ScriptableObject.CreateInstance<PlacementAssetDatabase>();

            var furniture = GetOrCreateCategory("Furniture", DataFolder + "/Category_Furniture.asset");
            var tech = GetOrCreateCategory("Tech", DataFolder + "/Category_Tech.asset");
            var decor = GetOrCreateCategory("Decor", DataFolder + "/Category_Decor.asset");

            var furnitureAssets = new[]
            {
                CreateAssetData("Chair", prefabs[0], furniture, DataFolder + "/Asset_Chair.asset"),
                CreateAssetData("Table", prefabs[1], furniture, DataFolder + "/Asset_Table.asset")
            };

            var techAssets = new[]
            {
                CreateAssetData("Lamp", prefabs[2], tech, DataFolder + "/Asset_Lamp.asset"),
                CreateAssetData("TV", prefabs[3], tech, DataFolder + "/Asset_TV.asset")
            };

            var decorAssets = new[]
            {
                CreateAssetData("Plant", prefabs[4], decor, DataFolder + "/Asset_Plant.asset"),
                CreateAssetData("Vase", prefabs[5], decor, DataFolder + "/Asset_Vase.asset")
            };

            SetCategoryAssets(furniture, furnitureAssets);
            SetCategoryAssets(tech, techAssets);
            SetCategoryAssets(decor, decorAssets);

            if (AssetDatabase.LoadAssetAtPath<PlacementAssetDatabase>(dbPath) == null)
                AssetDatabase.CreateAsset(database, dbPath);

            var serialized = new SerializedObject(database);
            var categoriesProp = serialized.FindProperty("categories");
            categoriesProp.arraySize = 3;
            categoriesProp.GetArrayElementAtIndex(0).objectReferenceValue = furniture;
            categoriesProp.GetArrayElementAtIndex(1).objectReferenceValue = tech;
            categoriesProp.GetArrayElementAtIndex(2).objectReferenceValue = decor;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static AssetCategory GetOrCreateCategory(string name, string path)
        {
            var category = AssetDatabase.LoadAssetAtPath<AssetCategory>(path);
            if (category == null)
                category = ScriptableObject.CreateInstance<AssetCategory>();

            var serialized = new SerializedObject(category);
            serialized.FindProperty("categoryName").stringValue = name;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (AssetDatabase.LoadAssetAtPath<AssetCategory>(path) == null)
                AssetDatabase.CreateAsset(category, path);

            EditorUtility.SetDirty(category);
            return category;
        }

        private static AssetData CreateAssetData(string name, GameObject prefab, AssetCategory category, string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<AssetData>(path);
            if (data == null)
                data = ScriptableObject.CreateInstance<AssetData>();

            var serialized = new SerializedObject(data);
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("categoryRef").objectReferenceValue = category;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (AssetDatabase.LoadAssetAtPath<AssetData>(path) == null)
                AssetDatabase.CreateAsset(data, path);

            data.SetCategory(category);
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SetCategoryAssets(AssetCategory category, AssetData[] assets)
        {
            var serialized = new SerializedObject(category);
            var assetsProp = serialized.FindProperty("assets");
            assetsProp.arraySize = assets.Length;
            for (var i = 0; i < assets.Length; i++)
                assetsProp.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(category);
        }

        private static (UIAssetSlot slot, CategorySectionUI section) CreateUiPrefabs()
        {
            var slotPath = PrefabFolder + "/UIAssetSlot.prefab";
            var sectionPath = PrefabFolder + "/CategorySection.prefab";

            UIAssetSlot slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(slotPath)?.GetComponent<UIAssetSlot>();
            CategorySectionUI sectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sectionPath)?.GetComponent<CategorySectionUI>();

            if (slotPrefab == null)
            {
                var slotRoot = CreateUiObject("UIAssetSlot", typeof(RectTransform), typeof(Image), typeof(UIAssetSlot), typeof(CanvasGroup));
                var slotRect = slotRoot.GetComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(96f, 110f);

                var slotImage = slotRoot.GetComponent<Image>();
                slotImage.color = new Color(0.18f, 0.2f, 0.24f, 0.95f);

                var icon = CreateUiObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(slotRoot.transform, false);
                var iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.1f, 0.35f);
                iconRect.anchorMax = new Vector2(0.9f, 0.95f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;

                var label = CreateUiObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(slotRoot.transform, false);
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0.3f);
                labelRect.offsetMin = new Vector2(4f, 4f);
                labelRect.offsetMax = new Vector2(-4f, 0f);
                var labelText = label.GetComponent<Text>();
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.fontSize = 12;
                labelText.color = Color.white;
                labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                var slotComponent = slotRoot.GetComponent<UIAssetSlot>();
                var slotSo = new SerializedObject(slotComponent);
                slotSo.FindProperty("iconImage").objectReferenceValue = icon.GetComponent<Image>();
                slotSo.FindProperty("labelText").objectReferenceValue = labelText;
                slotSo.ApplyModifiedPropertiesWithoutUndo();

                slotPrefab = PrefabUtility.SaveAsPrefabAsset(slotRoot, slotPath).GetComponent<UIAssetSlot>();
                Object.DestroyImmediate(slotRoot);
            }

            if (sectionPrefab == null)
            {
                var sectionRoot = CreateUiObject("CategorySection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(CategorySectionUI));
                var layout = sectionRoot.GetComponent<VerticalLayoutGroup>();
                layout.childControlHeight = true;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = true;
                layout.spacing = 4f;

                var header = CreateUiObject("Header", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
                header.transform.SetParent(sectionRoot.transform, false);
                var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
                headerLayout.padding = new RectOffset(8, 8, 6, 6);
                headerLayout.spacing = 8f;
                header.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
                var headerLE = header.AddComponent<LayoutElement>();
                headerLE.minHeight = 36f;

                var indicator = CreateUiObject("Indicator", typeof(RectTransform), typeof(Text));
                indicator.transform.SetParent(header.transform, false);
                var indicatorText = indicator.GetComponent<Text>();
                indicatorText.text = "▼";
                indicatorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                indicatorText.color = Color.white;
                indicatorText.fontSize = 14;

                var headerLabel = CreateUiObject("Title", typeof(RectTransform), typeof(Text));
                headerLabel.transform.SetParent(header.transform, false);
                var titleText = headerLabel.GetComponent<Text>();
                titleText.text = "Category";
                titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                titleText.color = Color.white;
                titleText.fontSize = 14;
                titleText.alignment = TextAnchor.MiddleLeft;

                var content = CreateUiObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
                content.transform.SetParent(sectionRoot.transform, false);
                var grid = content.GetComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(96f, 110f);
                grid.spacing = new Vector2(8f, 8f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
                grid.padding = new RectOffset(8, 8, 8, 8);

                var sectionComponent = sectionRoot.GetComponent<CategorySectionUI>();
                var sectionSo = new SerializedObject(sectionComponent);
                sectionSo.FindProperty("headerButton").objectReferenceValue = header.GetComponent<Button>();
                sectionSo.FindProperty("headerLabel").objectReferenceValue = titleText;
                sectionSo.FindProperty("contentRoot").objectReferenceValue = content.GetComponent<RectTransform>();
                sectionSo.FindProperty("grid").objectReferenceValue = grid;
                sectionSo.FindProperty("expandIndicator").objectReferenceValue = indicatorText;
                sectionSo.ApplyModifiedPropertiesWithoutUndo();

                sectionPrefab = PrefabUtility.SaveAsPrefabAsset(sectionRoot, sectionPath).GetComponent<CategorySectionUI>();
                Object.DestroyImmediate(sectionRoot);
            }

            return (slotPrefab, sectionPrefab);
        }

        private static GameObject CreateUiObject(string name, params System.Type[] components)
        {
            return new GameObject(name, components);
        }

        private static void ConfigureGroundPlane()
        {
            var plane = GameObject.Find("Plane");
            if (plane == null)
                return;

            plane.layer = LayerMask.NameToLayer("Ground");
            plane.isStatic = true;
        }

        private static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            camera.transform.position = new Vector3(0f, 12f, -12f);
            camera.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

            if (camera.GetComponent<FlyingCameraController>() == null)
                camera.gameObject.AddComponent<FlyingCameraController>();
        }

        private static GameObject CreateManagers(Material previewMaterial)
        {
            var root = GameObject.Find("PlacementSystem");
            if (root == null)
                root = new GameObject("PlacementSystem");

            if (root.GetComponent<PlacementManager>() == null)
                root.AddComponent<PlacementManager>();

            if (root.GetComponent<SelectionManager>() == null)
                root.AddComponent<SelectionManager>();

            if (root.GetComponent<DragPlacementHandler>() == null)
                root.AddComponent<DragPlacementHandler>();

            if (root.GetComponent<RuntimeTransformGizmo>() == null)
                root.AddComponent<RuntimeTransformGizmo>();

            if (root.GetComponent<RuntimeGizmoVisualizer>() == null)
                root.AddComponent<RuntimeGizmoVisualizer>();

            if (root.GetComponent<UIManager>() == null)
                root.AddComponent<UIManager>();

            var placementSo = new SerializedObject(root.GetComponent<PlacementManager>());
            placementSo.FindProperty("previewMaterial").objectReferenceValue = previewMaterial;
            placementSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject CreateUi(PlacementAssetDatabase database, (UIAssetSlot slot, CategorySectionUI section) uiPrefabs, GameObject managers)
        {
            var existingCanvas = Object.FindFirstObjectByType<Canvas>();
            if (existingCanvas != null && existingCanvas.GetComponent<UIManager>() != null)
                return existingCanvas.gameObject;

            if (existingCanvas != null)
                Object.DestroyImmediate(existingCanvas.gameObject);

            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
                Object.DestroyImmediate(eventSystem.gameObject);

            var canvasGo = CreateUiObject("PlacementUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var leftPanel = BuildLeftPanel(canvasGo.transform, uiPrefabs, database, managers);
            var rightPanel = BuildRightPanel(canvasGo.transform, managers);

            var uiManager = managers.GetComponent<UIManager>();
            var uiSo = new SerializedObject(uiManager);
            uiSo.FindProperty("leftPanel").objectReferenceValue = leftPanel.GetComponent<LeftPanelController>();
            uiSo.FindProperty("rightPanel").objectReferenceValue = rightPanel.GetComponent<RightPanelController>();
            uiSo.FindProperty("database").objectReferenceValue = database;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            return canvasGo;
        }

        private static GameObject BuildLeftPanel(Transform parent, (UIAssetSlot slot, CategorySectionUI section) uiPrefabs, PlacementAssetDatabase database, GameObject managers)
        {
            var panel = CreateUiObject("LeftPanel", typeof(RectTransform), typeof(Image), typeof(CollapsibleSidebar), typeof(LeftPanelController));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 0f);
            rect.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

            var toggle = CreateUiObject("ToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
            toggle.transform.SetParent(panel.transform, false);
            var toggleRect = toggle.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(1f, 0.5f);
            toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(0f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(8f, 0f);
            toggleRect.sizeDelta = new Vector2(28f, 80f);
            toggle.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.22f, 1f);

            var toggleLabelGo = CreateUiObject("Label", typeof(RectTransform), typeof(Text));
            toggleLabelGo.transform.SetParent(toggle.transform, false);
            var toggleLabel = toggleLabelGo.GetComponent<Text>();
            toggleLabel.text = "<";
            toggleLabel.alignment = TextAnchor.MiddleCenter;
            toggleLabel.color = Color.white;
            toggleLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            StretchFull(toggleLabelGo.GetComponent<RectTransform>());

            var scroll = CreateUiObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scroll.transform.SetParent(panel.transform, false);
            var scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(8f, 8f);
            scrollRect.offsetMax = new Vector2(-8f, -8f);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

            var viewport = CreateUiObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scroll.transform, false);
            StretchFull(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = CreateUiObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 8f;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollComponent = scroll.GetComponent<ScrollRect>();
            scrollComponent.viewport = viewport.GetComponent<RectTransform>();
            scrollComponent.content = contentRect;
            scrollComponent.horizontal = false;

            var sidebar = panel.GetComponent<CollapsibleSidebar>();
            var sidebarSo = new SerializedObject(sidebar);
            sidebarSo.FindProperty("panel").objectReferenceValue = rect;
            sidebarSo.FindProperty("toggleButton").objectReferenceValue = toggle.GetComponent<Button>();
            sidebarSo.FindProperty("toggleLabel").objectReferenceValue = toggleLabel;
            sidebarSo.FindProperty("hiddenOffset").floatValue = -320f;
            sidebarSo.ApplyModifiedPropertiesWithoutUndo();

            var leftController = panel.GetComponent<LeftPanelController>();
            var leftSo = new SerializedObject(leftController);
            leftSo.FindProperty("database").objectReferenceValue = database;
            leftSo.FindProperty("scrollRect").objectReferenceValue = scrollComponent;
            leftSo.FindProperty("categoryContainer").objectReferenceValue = contentRect;
            leftSo.FindProperty("categorySectionPrefab").objectReferenceValue = uiPrefabs.section;
            leftSo.FindProperty("assetSlotPrefab").objectReferenceValue = uiPrefabs.slot;
            leftSo.FindProperty("dragHandler").objectReferenceValue = managers.GetComponent<DragPlacementHandler>();
            leftSo.FindProperty("rootCanvas").objectReferenceValue = parent.GetComponent<Canvas>();
            leftSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static GameObject BuildRightPanel(Transform parent, GameObject managers)
        {
            var panel = CreateUiObject("RightPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(RightPanelController));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 0f);
            rect.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateUiText("Title", "Inspector", 18, TextAnchor.MiddleLeft);
            title.transform.SetParent(panel.transform, false);

            var modeRow = CreateUiObject("ModeRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            modeRow.transform.SetParent(panel.transform, false);
            var modeLayout = modeRow.GetComponent<HorizontalLayoutGroup>();
            modeLayout.spacing = 8f;
            modeLayout.childForceExpandWidth = true;

            var translateBtn = CreateUiButton("Translate", modeRow.transform);
            var rotateBtn = CreateUiButton("Rotate", modeRow.transform);

            var positionGroup = CreateVectorGroup(panel.transform, "Position", uniformScale: false);
            var rotationGroup = CreateVectorGroup(panel.transform, "Rotation", uniformScale: false);
            var scaleGroup = CreateVectorGroup(panel.transform, "Scale", uniformScale: true);

            var deleteBtn = CreateUiButton("Delete (Del)", panel.transform);
            deleteBtn.GetComponent<Image>().color = new Color(0.55f, 0.15f, 0.15f, 1f);

            var rightController = panel.GetComponent<RightPanelController>();
            var rightSo = new SerializedObject(rightController);
            rightSo.FindProperty("panelGroup").objectReferenceValue = panel.GetComponent<CanvasGroup>();
            rightSo.FindProperty("titleLabel").objectReferenceValue = title.GetComponent<Text>();
            rightSo.FindProperty("positionFields").objectReferenceValue = positionGroup.GetComponent<Vector3FieldGroup>();
            rightSo.FindProperty("rotationFields").objectReferenceValue = rotationGroup.GetComponent<Vector3FieldGroup>();
            rightSo.FindProperty("scaleFields").objectReferenceValue = scaleGroup.GetComponent<Vector3FieldGroup>();
            rightSo.FindProperty("translateModeButton").objectReferenceValue = translateBtn.GetComponent<Button>();
            rightSo.FindProperty("rotateModeButton").objectReferenceValue = rotateBtn.GetComponent<Button>();
            rightSo.FindProperty("deleteButton").objectReferenceValue = deleteBtn.GetComponent<Button>();
            rightSo.FindProperty("transformGizmo").objectReferenceValue = managers.GetComponent<RuntimeTransformGizmo>();
            rightSo.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static GameObject CreateVectorGroup(Transform parent, string label, bool uniformScale)
        {
            var root = CreateUiObject(label + "Group", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Vector3FieldGroup));
            root.transform.SetParent(parent, false);
            var rootLayout = root.GetComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 4f;

            var title = CreateUiText("Label", label, 14, TextAnchor.MiddleLeft);
            title.transform.SetParent(root.transform, false);

            var xRow = CreateAxisRow(root.transform, "X");
            var yRow = CreateAxisRow(root.transform, "Y");
            var zRow = CreateAxisRow(root.transform, "Z");

            var group = root.GetComponent<Vector3FieldGroup>();
            var so = new SerializedObject(group);
            so.FindProperty("xField").objectReferenceValue = xRow.GetComponentInChildren<InputField>();
            so.FindProperty("yField").objectReferenceValue = yRow.GetComponentInChildren<InputField>();
            so.FindProperty("zField").objectReferenceValue = zRow.GetComponentInChildren<InputField>();
            so.FindProperty("xMinus").objectReferenceValue = xRow.transform.Find("Minus").GetComponent<Button>();
            so.FindProperty("xPlus").objectReferenceValue = xRow.transform.Find("Plus").GetComponent<Button>();
            so.FindProperty("yMinus").objectReferenceValue = yRow.transform.Find("Minus").GetComponent<Button>();
            so.FindProperty("yPlus").objectReferenceValue = yRow.transform.Find("Plus").GetComponent<Button>();
            so.FindProperty("zMinus").objectReferenceValue = zRow.transform.Find("Minus").GetComponent<Button>();
            so.FindProperty("zPlus").objectReferenceValue = zRow.transform.Find("Plus").GetComponent<Button>();
            so.FindProperty("useUniformScale").boolValue = uniformScale;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject CreateAxisRow(Transform parent, string axis)
        {
            var row = CreateUiObject(axis + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childForceExpandWidth = true;

            var axisLabel = CreateUiText("Axis", axis, 13, TextAnchor.MiddleLeft);
            axisLabel.transform.SetParent(row.transform, false);
            axisLabel.GetComponent<LayoutElement>().minWidth = 16f;

            var minus = CreateUiButton("-", row.transform);
            minus.name = "Minus";
            minus.GetComponent<LayoutElement>().minWidth = 28f;

            var fieldGo = CreateUiObject("Field", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(InspectorInputFocus));
            fieldGo.transform.SetParent(row.transform, false);
            fieldGo.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.2f, 1f);
            var input = fieldGo.GetComponent<InputField>();
            input.contentType = InputField.ContentType.DecimalNumber;

            var text = CreateUiText("Text", "0", 13, TextAnchor.MiddleLeft);
            text.transform.SetParent(fieldGo.transform, false);
            StretchFull(text.GetComponent<RectTransform>());
            text.GetComponent<Text>().supportRichText = false;
            input.textComponent = text.GetComponent<Text>();

            var plus = CreateUiButton("+", row.transform);
            plus.name = "Plus";
            plus.GetComponent<LayoutElement>().minWidth = 28f;

            return row;
        }

        private static GameObject CreateUiText(string name, string text, int fontSize, TextAnchor anchor)
        {
            var go = CreateUiObject(name, typeof(RectTransform), typeof(Text));
            var label = go.GetComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.AddComponent<LayoutElement>().minHeight = 24f;
            return go;
        }

        private static GameObject CreateUiButton(string label, Transform parent)
        {
            var go = CreateUiObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.26f, 1f);
            go.GetComponent<LayoutElement>().minHeight = 28f;

            var text = CreateUiText("Text", label, 13, TextAnchor.MiddleCenter);
            text.transform.SetParent(go.transform, false);
            StretchFull(text.GetComponent<RectTransform>());
            return go;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void WireManagers(GameObject managers, GameObject canvas, Material previewMaterial)
        {
            var camera = Camera.main;
            var selection = managers.GetComponent<SelectionManager>();
            var drag = managers.GetComponent<DragPlacementHandler>();
            var gizmo = managers.GetComponent<RuntimeTransformGizmo>();

            var selectionSo = new SerializedObject(selection);
            selectionSo.FindProperty("sceneCamera").objectReferenceValue = camera;
            selectionSo.FindProperty("transformGizmo").objectReferenceValue = gizmo;
            selectionSo.ApplyModifiedPropertiesWithoutUndo();

            var dragSo = new SerializedObject(drag);
            dragSo.FindProperty("sceneCamera").objectReferenceValue = camera;
            dragSo.ApplyModifiedPropertiesWithoutUndo();

            var gizmoSo = new SerializedObject(gizmo);
            gizmoSo.FindProperty("sceneCamera").objectReferenceValue = camera;
            gizmoSo.ApplyModifiedPropertiesWithoutUndo();

            var placementSo = new SerializedObject(managers.GetComponent<PlacementManager>());
            placementSo.FindProperty("previewMaterial").objectReferenceValue = previewMaterial;
            placementSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(managers);
            EditorUtility.SetDirty(canvas);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
