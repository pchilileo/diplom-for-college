using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PlacementSystem.Editor
{
    /// <summary>
    /// Builds the editor UI (equipment library, inspector, status bar) with
    /// TextMeshPro in the Unity dark-skin style.
    ///
    /// Menu: <b>Placement System ▸ Rebuild UI</b>. The old "PlacementUI" canvas
    /// is deleted and created again, together with the UIAssetSlot and
    /// CategorySection prefabs. Colours come from <see cref="UITheme"/>.
    /// </summary>
    public static class PlacementUIBuilder
    {
        private const string PrefabFolder = "Assets/PlacementSystem/Prefabs";
        private const string DatabasePath = "Assets/PlacementSystem/Data/PlacementAssetDatabase.asset";
        private const string CanvasName   = "PlacementUI";

        private const float LeftWidth   = 270f;
        private const float RightWidth  = 330f;
        private const float StatusHeight = 26f;
        private const float TabHeight   = 26f;

        private static TMP_FontAsset font;
        private static Sprite rounded;
        private static Sprite arrowSprite;

        // ── Entry point ───────────────────────────────────────────────────────

        [MenuItem("Placement System/Rebuild UI")]
        public static void RebuildUi()
        {
            var managers = Object.FindAnyObjectByType<SelectionManager>()?.gameObject;
            if (managers == null)
            {
                EditorUtility.DisplayDialog("Placement System",
                    "В сцене нет объекта с SelectionManager. Сначала выполните Placement System ▸ Setup Scene.", "OK");
                return;
            }

            var database = AssetDatabase.LoadAssetAtPath<PlacementAssetDatabase>(DatabasePath);
            Build(managers, database);

            EditorSceneManager.MarkSceneDirty(managers.scene);
            Debug.Log("Placement System: UI rebuilt.");
        }

        public static GameObject Build(GameObject managers, PlacementAssetDatabase database)
        {
            LoadResources();

            var slotPrefab = BuildSlotPrefab();
            var sectionPrefab = BuildSectionPrefab();

            DeleteOldCanvas();
            EnsureEventSystem();

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Rebuild UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var left = BuildLeftPanel(canvasGo.transform, managers, database, slotPrefab, sectionPrefab);
            var right = BuildRightPanel(canvasGo.transform, managers);
            BuildStatusBar(canvasGo.transform);
            BuildCheckUi(canvasGo.transform, managers);

            var uiManager = managers.GetComponent<UIManager>();
            if (uiManager != null)
            {
                var so = new SerializedObject(uiManager);
                so.FindProperty("leftPanel").objectReferenceValue = left;
                so.FindProperty("rightPanel").objectReferenceValue = right;
                so.FindProperty("database").objectReferenceValue = database;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return canvasGo;
        }

        private static void LoadResources()
        {
            font = TMP_Settings.defaultFontAsset;
            rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            arrowSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        }

        private static void DeleteOldCanvas()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (canvas == null || !canvas.isRootCanvas)
                    continue;

                if (canvas.name == CanvasName || canvas.GetComponentInChildren<LeftPanelController>(true) != null)
                    Undo.DestroyObjectImmediate(canvas.gameObject);
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Rebuild UI");
        }

        // ── Library item / category prefabs ───────────────────────────────────

        private static UIAssetSlot BuildSlotPrefab()
        {
            var root = NewUi("UIAssetSlot", null);
            var bg = root.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = Color.clear;
            root.AddComponent<CanvasGroup>();
            var slot = root.AddComponent<UIAssetSlot>();

            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 6, 3, 3);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(layout, true, true, false, false);
            Element(root, minHeight: 40f);

            var frame = NewUi("IconFrame", root.transform);
            var frameImage = frame.AddComponent<Image>();
            frameImage.sprite = rounded;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = UITheme.FieldBackground;
            frameImage.raycastTarget = false;
            Element(frame, prefWidth: 34f, prefHeight: 34f);

            var icon = NewUi("Icon", frame.transform);
            Stretch(icon, 2f);
            var iconImage = icon.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var label = Text(root.transform, "Label", "", 12f, UITheme.Text, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.lineSpacing = -8f;
            Element(label.gameObject, prefHeight: 34f, flexWidth: 1f);

            var so = new SerializedObject(slot);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("labelText").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, "UIAssetSlot").GetComponent<UIAssetSlot>();
        }

        private static CategorySectionUI BuildSectionPrefab()
        {
            var root = NewUi("CategorySection", null);
            var section = root.AddComponent<CategorySectionUI>();
            var layout = root.AddComponent<VerticalLayoutGroup>();
            SetControl(layout, true, true, true, false);

            var header = NewUi("Header", root.transform);
            var headerImage = header.AddComponent<Image>();
            var headerButton = header.AddComponent<Button>();
            StyleButton(headerButton, headerImage, UITheme.SectionHeader, UITheme.Hover, UITheme.ButtonPressed);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(8, 10, 0, 0);
            headerLayout.spacing = 6f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(headerLayout, true, true, false, false);
            Element(header, minHeight: 26f);

            var arrow = Arrow(header.transform);
            var title = Text(header.transform, "Title", "", 12f, UITheme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Element(title.gameObject, flexWidth: 1f);
            var count = Text(header.transform, "Count", "0", 11f, UITheme.TextDim, TextAlignmentOptions.MidlineRight);

            var content = NewUi("Content", root.transform);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(4, 4, 2, 4);
            contentLayout.spacing = 1f;
            SetControl(contentLayout, true, true, true, false);

            var so = new SerializedObject(section);
            so.FindProperty("headerButton").objectReferenceValue = headerButton;
            so.FindProperty("headerLabel").objectReferenceValue = title;
            so.FindProperty("countLabel").objectReferenceValue = count;
            so.FindProperty("arrow").objectReferenceValue = arrow;
            so.FindProperty("contentRoot").objectReferenceValue = content.GetComponent<RectTransform>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, "CategorySection").GetComponent<CategorySectionUI>();
        }

        // ── Left panel: equipment library ─────────────────────────────────────

        private static LeftPanelController BuildLeftPanel(Transform canvas, GameObject managers, PlacementAssetDatabase database,
            UIAssetSlot slotPrefab, CategorySectionUI sectionPrefab)
        {
            var panel = NewUi("LeftPanel", canvas);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(0f, StatusHeight);
            rect.offsetMax = new Vector2(LeftWidth, 0f);
            panel.AddComponent<Image>().color = UITheme.PanelBackground;
            SubCanvas(panel);

            TabBar(panel.transform, "LIBRARY_TITLE");

            // Search
            var search = InputField(panel.transform, "Search", "", TextAlignmentOptions.MidlineLeft, 12f);
            Localize((TMP_Text)search.placeholder, "LIBRARY_SEARCH_PLACEHOLDER");
            search.contentType = TMP_InputField.ContentType.Standard;
            var searchRect = search.GetComponent<RectTransform>();
            searchRect.anchorMin = new Vector2(0f, 1f);
            searchRect.anchorMax = new Vector2(1f, 1f);
            searchRect.pivot = new Vector2(0.5f, 1f);
            searchRect.offsetMin = new Vector2(8f, -(TabHeight + 6f + 24f));
            searchRect.offsetMax = new Vector2(-8f, -(TabHeight + 6f));

            // Scrollable list
            var scrollGo = NewUi("ScrollView", panel.transform);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(0f, 4f);
            scrollRect.offsetMax = new Vector2(0f, -(TabHeight + 6f + 24f + 6f));
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = NewUi("Viewport", scrollGo.transform);
            Stretch(viewport);
            viewport.AddComponent<RectMask2D>();
            viewport.AddComponent<Image>().color = Color.clear;   // catches wheel / drag events

            var content = NewUi("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(6, 6, 0, 6);
            contentLayout.spacing = 4f;
            SetControl(contentLayout, true, true, true, false);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = Scrollbar(scrollGo.transform);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 0f;

            var noResults = Localize(Text(scrollGo.transform, "NoResults", "", 12f, UITheme.TextDim, TextAlignmentOptions.Top), "LIBRARY_NO_RESULTS");
            var noResultsRect = noResults.rectTransform;
            noResultsRect.anchorMin = new Vector2(0f, 1f);
            noResultsRect.anchorMax = new Vector2(1f, 1f);
            noResultsRect.pivot = new Vector2(0.5f, 1f);
            noResultsRect.offsetMin = new Vector2(0f, -40f);
            noResultsRect.offsetMax = new Vector2(0f, -12f);
            noResults.gameObject.SetActive(false);

            SidebarTab(panel, CollapsibleSidebar.Side.Left);

            var controller = panel.AddComponent<LeftPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("categoryContainer").objectReferenceValue = contentRect;
            so.FindProperty("categorySectionPrefab").objectReferenceValue = sectionPrefab;
            so.FindProperty("assetSlotPrefab").objectReferenceValue = slotPrefab;
            so.FindProperty("dragHandler").objectReferenceValue = managers.GetComponent<DragPlacementHandler>();
            so.FindProperty("searchField").objectReferenceValue = search;
            so.FindProperty("noResultsLabel").objectReferenceValue = noResults.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        // ── Right panel: inspector ────────────────────────────────────────────

        private static RightPanelController BuildRightPanel(Transform canvas, GameObject managers)
        {
            var panel = NewUi("RightPanel", canvas);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-RightWidth, StatusHeight);
            rect.offsetMax = new Vector2(0f, 0f);
            panel.AddComponent<Image>().color = UITheme.PanelBackground;
            SubCanvas(panel);

            TabBar(panel.transform, "INSPECTOR_TITLE");

            // Nothing selected
            var empty = Localize(Text(panel.transform, "EmptyState", "", 12f, UITheme.TextDim, TextAlignmentOptions.Top),
                "INSPECTOR_EMPTY");
            empty.textWrappingMode = TextWrappingModes.Normal;
            var emptyRect = empty.rectTransform;
            emptyRect.anchorMin = new Vector2(0f, 1f);
            emptyRect.anchorMax = new Vector2(1f, 1f);
            emptyRect.pivot = new Vector2(0.5f, 1f);
            emptyRect.offsetMin = new Vector2(16f, -(TabHeight + 80f));
            emptyRect.offsetMax = new Vector2(-16f, -(TabHeight + 30f));

            // Selected object
            var content = NewUi("Content", panel.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(0f, 48f);
            contentRect.offsetMax = new Vector2(0f, -TabHeight);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            SetControl(layout, true, true, true, false);

            // Object header: icon + name + category
            var header = NewUi("ObjectHeader", content.transform);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(10, 10, 10, 10);
            headerLayout.spacing = 10f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(headerLayout, true, true, false, false);

            var frame = NewUi("IconFrame", header.transform);
            var frameImage = frame.AddComponent<Image>();
            frameImage.sprite = rounded;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = UITheme.FieldBackground;
            Element(frame, prefWidth: 44f, prefHeight: 44f);
            var icon = NewUi("Icon", frame.transform);
            Stretch(icon, 3f);
            var iconImage = icon.AddComponent<Image>();
            iconImage.preserveAspect = true;

            var names = NewUi("Names", header.transform);
            var namesLayout = names.AddComponent<VerticalLayoutGroup>();
            namesLayout.spacing = 2f;
            namesLayout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(namesLayout, true, true, true, false);
            Element(names, flexWidth: 1f);
            var title = Text(names.transform, "Title", "", 14f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.Normal;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.maxVisibleLines = 2;
            var subtitle = Text(names.transform, "Subtitle", "", 11f, UITheme.TextDim, TextAlignmentOptions.MidlineLeft);

            Divider(content.transform);

            // Transform
            var transformBody = Foldout(content.transform, "INSPECTOR_TRANSFORM", out var transformHeader);
            var reset = SmallButton(transformHeader.transform, "INSPECTOR_RESET", 48f);
            var position = VectorRow(transformBody, "INSPECTOR_POSITION", false, 0.02f, float.MinValue, "0.##");
            var rotation = VectorRow(transformBody, "INSPECTOR_ROTATION", false, 0.5f, float.MinValue, "0.#");
            var scale = VectorRow(transformBody, "INSPECTOR_SCALE", true, 0.005f, 0.01f, "0.###");
            var note = Localize(Text(transformBody, "Note", "", 10f, UITheme.TextDim, TextAlignmentOptions.TopLeft),
                "INSPECTOR_TRANSFORM_NOTE");
            note.textWrappingMode = TextWrappingModes.Normal;

            // Tool
            var toolBody = Foldout(content.transform, "INSPECTOR_TOOL", out _);
            var toolRow = NewUi("ToolRow", toolBody);
            var toolLayout = toolRow.AddComponent<HorizontalLayoutGroup>();
            toolLayout.spacing = 2f;
            // No force-expand on height: a group with childForceExpandHeight reports
            // itself as flexible, and the inspector column would give it all spare space.
            SetControl(toolLayout, true, true, true, false);
            Element(toolRow, minHeight: 24f);
            var translateButton = LocalizeButton(TextButton(toolRow.transform, "TranslateButton", "", 12f), "TOOL_TRANSLATE", suffix: "  <color=#8F8F8F>T</color>");
            var rotateButton = LocalizeButton(TextButton(toolRow.transform, "RotateButton", "", 12f), "TOOL_ROTATE", suffix: "  <color=#8F8F8F>R</color>");
            var scaleButton = LocalizeButton(TextButton(toolRow.transform, "ScaleButton", "", 12f), "TOOL_SCALE", suffix: "  <color=#8F8F8F>Y</color>");
            Element(translateButton.gameObject, prefHeight: 24f, flexWidth: 1f);
            Element(rotateButton.gameObject, prefHeight: 24f, flexWidth: 1f);
            Element(scaleButton.gameObject, prefHeight: 24f, flexWidth: 1f);
            var toolNote = Localize(Text(toolBody, "Note", "", 10f, UITheme.TextDim, TextAlignmentOptions.TopLeft),
                "INSPECTOR_TOOL_NOTE");
            toolNote.textWrappingMode = TextWrappingModes.Normal;

            // Connections
            var connectionsBody = Foldout(content.transform, "INSPECTOR_CONNECTIONS", out _);
            var connectors = InfoRow(connectionsBody, "INSPECTOR_CONNECTORS");
            var wires = InfoRow(connectionsBody, "INSPECTOR_WIRES");

            // Delete, pinned to the bottom
            var delete = LocalizeButton(TextButton(panel.transform, "DeleteButton", "", 12f), "INSPECTOR_DELETE", suffix: "   <color=#E0A0A0>Del</color>");
            StyleButton(delete, delete.GetComponent<Image>(), UITheme.Danger, UITheme.DangerHover, UITheme.ButtonPressed);
            var deleteRect = delete.GetComponent<RectTransform>();
            deleteRect.anchorMin = new Vector2(0f, 0f);
            deleteRect.anchorMax = new Vector2(1f, 0f);
            deleteRect.pivot = new Vector2(0.5f, 0f);
            deleteRect.offsetMin = new Vector2(10f, 10f);
            deleteRect.offsetMax = new Vector2(-10f, 38f);

            SidebarTab(panel, CollapsibleSidebar.Side.Right);

            var controller = panel.AddComponent<RightPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("emptyState").objectReferenceValue = empty.gameObject;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("subtitleLabel").objectReferenceValue = subtitle;
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("positionFields").objectReferenceValue = position;
            so.FindProperty("rotationFields").objectReferenceValue = rotation;
            so.FindProperty("scaleFields").objectReferenceValue = scale;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("translateModeButton").objectReferenceValue = translateButton;
            so.FindProperty("rotateModeButton").objectReferenceValue = rotateButton;
            so.FindProperty("scaleModeButton").objectReferenceValue = scaleButton;
            so.FindProperty("transformGizmo").objectReferenceValue = managers.GetComponent<RuntimeTransformGizmo>();
            so.FindProperty("connectorsValue").objectReferenceValue = connectors;
            so.FindProperty("wiresValue").objectReferenceValue = wires;
            so.FindProperty("deleteButton").objectReferenceValue = delete;
            so.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        /// <summary>Inspector row: label, optional link button and X / Y / Z fields.</summary>
        private static Vector3FieldGroup VectorRow(Transform parent, string label, bool withLink,
            float scrubSpeed, float minValue, string format)
        {
            var row = NewUi(label + "Row", parent);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(layout, true, true, false, false);
            Element(row, minHeight: 20f);

            var title = Localize(Text(row.transform, "Label", "", 12f, UITheme.Text, TextAlignmentOptions.MidlineLeft), label);
            Element(title.gameObject, prefWidth: 62f);

            Button linkButton = null;
            TMP_Text linkText = null;
            if (withLink)
            {
                linkButton = TextButton(row.transform, "LinkButton", "1:1", 9f);
                linkText = linkButton.GetComponentInChildren<TMP_Text>();
                Element(linkButton.gameObject, prefWidth: 22f, prefHeight: 18f);
            }
            else
            {
                Element(NewUi("LinkSpacer", row.transform), prefWidth: 22f);
            }

            var handles = new AxisScrubHandle[3];
            var fields = new TMP_InputField[3];
            string[] axes = { "X", "Y", "Z" };
            Color[] colors = { UITheme.AxisX, UITheme.AxisY, UITheme.AxisZ };

            for (var i = 0; i < 3; i++)
            {
                var cell = NewUi(axes[i], row.transform);
                var cellLayout = cell.AddComponent<HorizontalLayoutGroup>();
                cellLayout.spacing = 3f;
                cellLayout.childAlignment = TextAnchor.MiddleLeft;
                SetControl(cellLayout, true, true, false, false);
                Element(cell, prefWidth: 60f, flexWidth: 1f);

                var axisLabel = Text(cell.transform, "Axis", axes[i], 11f, colors[i], TextAlignmentOptions.Center, FontStyles.Bold);
                axisLabel.raycastTarget = true;
                Element(axisLabel.gameObject, prefWidth: 11f, prefHeight: 20f);
                handles[i] = axisLabel.gameObject.AddComponent<AxisScrubHandle>();

                fields[i] = InputField(cell.transform, "Field", "0", TextAlignmentOptions.MidlineLeft, 12f);
                fields[i].contentType = TMP_InputField.ContentType.DecimalNumber;
                Element(fields[i].gameObject, prefWidth: 40f, prefHeight: 20f, flexWidth: 1f);
            }

            var group = row.AddComponent<Vector3FieldGroup>();
            var so = new SerializedObject(group);
            so.FindProperty("xField").objectReferenceValue = fields[0];
            so.FindProperty("yField").objectReferenceValue = fields[1];
            so.FindProperty("zField").objectReferenceValue = fields[2];
            so.FindProperty("xHandle").objectReferenceValue = handles[0];
            so.FindProperty("yHandle").objectReferenceValue = handles[1];
            so.FindProperty("zHandle").objectReferenceValue = handles[2];
            so.FindProperty("linkButton").objectReferenceValue = linkButton;
            so.FindProperty("linkGraphic").objectReferenceValue = linkText;
            so.FindProperty("scrubSpeed").floatValue = scrubSpeed;
            so.FindProperty("minValue").floatValue = minValue;
            so.FindProperty("format").stringValue = format;
            so.ApplyModifiedPropertiesWithoutUndo();

            return group;
        }

        private static TMP_Text InfoRow(Transform parent, string label)
        {
            var row = NewUi(label, parent);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(layout, true, true, false, false);
            Element(row, minHeight: 18f);

            var title = Localize(Text(row.transform, "Label", "", 12f, UITheme.Text, TextAlignmentOptions.MidlineLeft), label);
            Element(title.gameObject, flexWidth: 1f);
            return Text(row.transform, "Value", "0", 12f, Color.white, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        }

        // ── Status bar ────────────────────────────────────────────────────────

        private static void BuildStatusBar(Transform canvas)
        {
            var bar = NewUi("StatusBar", canvas);
            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, StatusHeight);
            bar.AddComponent<Image>().color = UITheme.WindowTab;
            SubCanvas(bar);

            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 12, 2, 2);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(layout, true, true, false, true);

            var normal = ModeTab(bar.transform, "1", "MODE_OBJECTS");
            var connect = ModeTab(bar.transform, "2", "MODE_WIRE_CONNECT");
            var remove = ModeTab(bar.transform, "3", "MODE_WIRE_DELETE");

            Element(NewUi("Spacer", bar.transform), flexWidth: 1f);

            var message = Text(bar.transform, "Message", "", 12f, UITheme.AccentBright, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            message.margin = new Vector4(0f, 0f, 24f, 0f);   // gap before the hint
            var hint = Text(bar.transform, "Hint", "", 11f, UITheme.TextDim, TextAlignmentOptions.MidlineRight);

            // Language switch (cycles through the *.lang files)
            Element(NewUi("LanguageGap", bar.transform), prefWidth: 10f);
            var language = ModeTab(bar.transform, "", null);
            language.name = "LanguageButton";
            var languageLabel = language.GetComponentInChildren<TMP_Text>();
            languageLabel.text = Loc.Language.ToUpperInvariant();
            languageLabel.fontStyle = FontStyles.Bold;

            var controller = bar.AddComponent<StatusBarController>();
            var so = new SerializedObject(controller);
            so.FindProperty("modeManager").objectReferenceValue = Object.FindAnyObjectByType<EditorModeManager>();
            so.FindProperty("normalModeButton").objectReferenceValue = normal;
            so.FindProperty("wireConnectModeButton").objectReferenceValue = connect;
            so.FindProperty("wireDeleteModeButton").objectReferenceValue = remove;
            so.FindProperty("hintLabel").objectReferenceValue = hint;
            so.FindProperty("messageLabel").objectReferenceValue = message;
            so.FindProperty("languageButton").objectReferenceValue = language;
            so.FindProperty("languageLabel").objectReferenceValue = languageLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <param name="hotkey">Grey key shown before the text ("1", "2"…), may be empty.</param>
        /// <param name="labelKey">Language key of the text; null = the caller sets the text.</param>
        private static Button ModeTab(Transform parent, string hotkey, string labelKey)
        {
            var go = NewUi(labelKey ?? "Tab", parent);
            var image = go.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            var button = go.AddComponent<Button>();
            StyleButton(button, image, Color.clear, UITheme.Hover, UITheme.ButtonPressed);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            SetControl(layout, true, true, false, true);

            var text = Text(go.transform, "Text", "", 12f, UITheme.Text, TextAlignmentOptions.Center);
            if (labelKey != null)
                Localize(text, labelKey, prefix: string.IsNullOrEmpty(hotkey) ? null : $"<color=#8F8F8F>{hotkey}</color>   ");
            return button;
        }

        // ── Wiring check: F2 window + banner ──────────────────────────────────

        private static void BuildCheckUi(Transform canvas, GameObject managers)
        {
            var checkManager = managers.GetComponent<SubstationCheckManager>();
            if (checkManager == null)
                checkManager = Undo.AddComponent<SubstationCheckManager>(managers);
            var managerSo = new SerializedObject(checkManager);
            managerSo.FindProperty("modeManager").objectReferenceValue = Object.FindAnyObjectByType<EditorModeManager>();
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            var root = NewUi("CheckModeUI", canvas);
            Stretch(root);
            // Above the side panels, both for drawing and for clicks.
            var rootCanvas = root.AddComponent<Canvas>();
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 100;
            root.AddComponent<GraphicRaycaster>();

            // Banner shown while checking
            var banner = NewUi("Banner", root.transform);
            var bannerRect = banner.GetComponent<RectTransform>();
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -10f);
            bannerRect.sizeDelta = new Vector2(720f, 0f);
            var bannerImage = banner.AddComponent<Image>();
            bannerImage.sprite = rounded;
            bannerImage.type = Image.Type.Sliced;
            bannerImage.color = UITheme.Hex(0x202020, 0.95f);
            var bannerLayout = banner.AddComponent<VerticalLayoutGroup>();
            bannerLayout.padding = new RectOffset(16, 12, 10, 12);
            bannerLayout.spacing = 4f;
            SetControl(bannerLayout, true, true, true, false);
            banner.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleRow = NewUi("TitleRow", banner.transform);
            var titleLayout = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleLayout.spacing = 10f;
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(titleLayout, true, true, false, false);
            var bannerTitle = Text(titleRow.transform, "Title", "", 14f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Element(bannerTitle.gameObject, flexWidth: 1f);
            var bannerExit = LocalizeButton(TextButton(titleRow.transform, "ExitButton", "", 12f), "BANNER_EXIT");
            Element(bannerExit.gameObject, prefWidth: 160f, prefHeight: 24f);

            var bannerStats = Text(banner.transform, "Stats", "", 13f, UITheme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            var bannerDetails = Text(banner.transform, "Details", "", 11f, UITheme.TextDim, TextAlignmentOptions.TopLeft);
            bannerDetails.textWrappingMode = TextWrappingModes.Normal;

            // F2 window
            var menu = NewUi("Menu", root.transform);
            Stretch(menu);

            var dim = NewUi("Dim", menu.transform);
            Stretch(dim);
            var dimImage = dim.AddComponent<Image>();
            var dimButton = dim.AddComponent<Button>();   // click outside the window closes it
            var dimColor = new Color(0f, 0f, 0f, 0.55f);
            StyleButtonColors(dimButton, dimImage, dimColor, dimColor, dimColor);

            var window = NewUi("Window", menu.transform);
            var windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(440f, 0f);
            var windowImage = window.AddComponent<Image>();
            windowImage.sprite = rounded;
            windowImage.type = Image.Type.Sliced;
            windowImage.color = UITheme.PanelBackground;
            var windowLayout = window.AddComponent<VerticalLayoutGroup>();
            windowLayout.padding = new RectOffset(18, 18, 16, 18);
            windowLayout.spacing = 8f;
            SetControl(windowLayout, true, true, true, false);
            window.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Main page
            var mainPage = Page(window.transform, "MainPage", "CHECK_MENU_TITLE");
            var status = Text(mainPage.transform, "Status", "", 12f, UITheme.TextDim, TextAlignmentOptions.TopLeft);
            status.textWrappingMode = TextWrappingModes.Normal;
            Element(NewUi("Gap", mainPage.transform), prefHeight: 4f);
            var save = MenuButton(mainPage.transform, "SaveButton", "CHECK_SAVE");
            var load = MenuButton(mainPage.transform, "LoadButton", "CHECK_LOAD");
            var journal = MenuButton(mainPage.transform, "JournalButton", "CHECK_JOURNAL");
            var exitCheck = MenuButton(mainPage.transform, "ExitCheckButton", "CHECK_EXIT");
            StyleButton(exitCheck, exitCheck.GetComponent<Image>(), UITheme.Accent, UITheme.AccentBright, UITheme.ButtonPressed);
            var close = MenuButton(mainPage.transform, "CloseButton", "COMMON_CLOSE", "   <color=#8F8F8F>Esc</color>");

            // ── Save page: mode + exam password
            var savePage = Page(window.transform, "SavePage", "SAVE_TITLE");
            var saveHint = Localize(Text(savePage.transform, "Hint", "", 12f, UITheme.TextDim, TextAlignmentOptions.TopLeft),
                "SAVE_HINT");
            saveHint.textWrappingMode = TextWrappingModes.Normal;

            var modeRow = NewUi("ModeRow", savePage.transform);
            var modeLayout = modeRow.AddComponent<HorizontalLayoutGroup>();
            modeLayout.spacing = 2f;
            SetControl(modeLayout, true, true, true, false);
            var trainingMode = LocalizeButton(TextButton(modeRow.transform, "TrainingButton", "", 13f), "MODE_TRAINING");
            var examMode = LocalizeButton(TextButton(modeRow.transform, "ExamButton", "", 13f), "MODE_EXAM");
            Element(trainingMode.gameObject, prefHeight: 30f, flexWidth: 1f);
            Element(examMode.gameObject, prefHeight: 30f, flexWidth: 1f);

            var passwordGroup = NewUi("PasswordGroup", savePage.transform);
            var passwordGroupLayout = passwordGroup.AddComponent<VerticalLayoutGroup>();
            passwordGroupLayout.spacing = 4f;
            SetControl(passwordGroupLayout, true, true, true, false);
            Localize(Text(passwordGroup.transform, "Label", "", 12f, UITheme.Text, TextAlignmentOptions.MidlineLeft), "SAVE_PASSWORD_LABEL");
            var savePassword = PasswordField(passwordGroup.transform, "PasswordField", "SAVE_PASSWORD_PLACEHOLDER");

            var saveError = ErrorText(savePage.transform);
            var saveButtons = ButtonRow(savePage.transform);
            var saveBack = LocalizeButton(TextButton(saveButtons, "BackButton", "", 13f), "COMMON_BACK");
            var saveConfirm = LocalizeButton(TextButton(saveButtons, "SaveButton", "", 13f), "SAVE_CONFIRM");
            StyleButton(saveConfirm, saveConfirm.GetComponent<Image>(), UITheme.Accent, UITheme.AccentBright, UITheme.ButtonPressed);
            Element(saveBack.gameObject, prefHeight: 32f, prefWidth: 100f);
            Element(saveConfirm.gameObject, prefHeight: 32f, flexWidth: 1f);

            // ── Password page (loading an exam reference)
            var passwordPage = Page(window.transform, "PasswordPage", "MODE_EXAM");
            var passwordInfo = Text(passwordPage.transform, "Info", "", 12f, UITheme.TextDim, TextAlignmentOptions.TopLeft);
            passwordInfo.textWrappingMode = TextWrappingModes.Normal;
            var loadPassword = PasswordField(passwordPage.transform, "PasswordField", "PASSWORD_PLACEHOLDER");
            var passwordError = ErrorText(passwordPage.transform);
            var passwordButtons = ButtonRow(passwordPage.transform);
            var passwordCancel = LocalizeButton(TextButton(passwordButtons, "CancelButton", "", 13f), "COMMON_CANCEL");
            var passwordConfirm = LocalizeButton(TextButton(passwordButtons, "ConfirmButton", "", 13f), "PASSWORD_CONFIRM");
            StyleButton(passwordConfirm, passwordConfirm.GetComponent<Image>(), UITheme.Accent, UITheme.AccentBright, UITheme.ButtonPressed);
            Element(passwordCancel.gameObject, prefHeight: 32f, prefWidth: 100f);
            Element(passwordConfirm.gameObject, prefHeight: 32f, flexWidth: 1f);

            // ── Journal page
            var journalPage = Page(window.transform, "JournalPage", "CHECK_JOURNAL");
            var journalWarning = Text(journalPage.transform, "Warning", "", 12f, SubstationCheckManager.WrongColor, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            journalWarning.textWrappingMode = TextWrappingModes.Normal;
            var journalText = JournalList(journalPage.transform);
            var journalBack = MenuButton(journalPage.transform, "BackButton", "COMMON_BACK");

            var controller = root.AddComponent<CheckModeController>();
            var so = new SerializedObject(controller);
            so.FindProperty("checkManager").objectReferenceValue = checkManager;
            so.FindProperty("menu").objectReferenceValue = menu;
            so.FindProperty("dimButton").objectReferenceValue = dimButton;

            so.FindProperty("mainPage").objectReferenceValue = mainPage;
            so.FindProperty("menuStatus").objectReferenceValue = status;
            so.FindProperty("saveButton").objectReferenceValue = save;
            so.FindProperty("loadButton").objectReferenceValue = load;
            so.FindProperty("journalButton").objectReferenceValue = journal;
            so.FindProperty("exitCheckButton").objectReferenceValue = exitCheck;
            so.FindProperty("closeButton").objectReferenceValue = close;

            so.FindProperty("savePage").objectReferenceValue = savePage;
            so.FindProperty("trainingModeButton").objectReferenceValue = trainingMode;
            so.FindProperty("examModeButton").objectReferenceValue = examMode;
            so.FindProperty("savePasswordGroup").objectReferenceValue = passwordGroup;
            so.FindProperty("savePasswordField").objectReferenceValue = savePassword;
            so.FindProperty("saveError").objectReferenceValue = saveError;
            so.FindProperty("saveConfirmButton").objectReferenceValue = saveConfirm;
            so.FindProperty("saveBackButton").objectReferenceValue = saveBack;

            so.FindProperty("passwordPage").objectReferenceValue = passwordPage;
            so.FindProperty("passwordInfo").objectReferenceValue = passwordInfo;
            so.FindProperty("loadPasswordField").objectReferenceValue = loadPassword;
            so.FindProperty("passwordError").objectReferenceValue = passwordError;
            so.FindProperty("passwordConfirmButton").objectReferenceValue = passwordConfirm;
            so.FindProperty("passwordCancelButton").objectReferenceValue = passwordCancel;

            so.FindProperty("journalPage").objectReferenceValue = journalPage;
            so.FindProperty("journalWarning").objectReferenceValue = journalWarning;
            so.FindProperty("journalText").objectReferenceValue = journalText;
            so.FindProperty("journalBackButton").objectReferenceValue = journalBack;

            so.FindProperty("banner").objectReferenceValue = banner;
            so.FindProperty("bannerTitle").objectReferenceValue = bannerTitle;
            so.FindProperty("bannerStats").objectReferenceValue = bannerStats;
            so.FindProperty("bannerDetails").objectReferenceValue = bannerDetails;
            so.FindProperty("bannerExitButton").objectReferenceValue = bannerExit;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Hidden until needed (the controller itself stays active to catch F2).
            savePage.SetActive(false);
            passwordPage.SetActive(false);
            journalPage.SetActive(false);
            saveError.gameObject.SetActive(false);
            passwordError.gameObject.SetActive(false);
            menu.SetActive(false);
            banner.SetActive(false);
        }

        private static Button MenuButton(Transform parent, string name, string labelKey, string suffix = null)
        {
            var button = LocalizeButton(TextButton(parent, name, "", 13f), labelKey, suffix: suffix);
            Element(button.gameObject, prefHeight: 32f);
            return button;
        }

        /// <summary>One page of the F2 window: a vertical stack with a title.</summary>
        private static GameObject Page(Transform window, string name, string titleKey)
        {
            var page = NewUi(name, window);
            var layout = page.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            SetControl(layout, true, true, true, false);
            Localize(Text(page.transform, "Title", "", 16f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold), titleKey);
            return page;
        }

        private static Transform ButtonRow(Transform parent)
        {
            var row = NewUi("Buttons", parent);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            SetControl(layout, true, true, false, false);
            return row.transform;
        }

        private static TMP_InputField PasswordField(Transform parent, string name, string placeholderKey)
        {
            var field = InputField(parent, name, "", TextAlignmentOptions.MidlineLeft, 13f);
            Localize((TMP_Text)field.placeholder, placeholderKey);
            field.contentType = TMP_InputField.ContentType.Password;
            field.lineType = TMP_InputField.LineType.SingleLine;
            Element(field.gameObject, prefHeight: 30f);
            return field;
        }

        private static TextMeshProUGUI ErrorText(Transform parent)
        {
            var error = Text(parent, "Error", "", 12f, SubstationCheckManager.WrongColor, TextAlignmentOptions.MidlineLeft);
            error.textWrappingMode = TextWrappingModes.Normal;
            return error;
        }

        /// <summary>Scrollable rich-text list for the journal.</summary>
        private static TextMeshProUGUI JournalList(Transform parent)
        {
            var scrollGo = NewUi("JournalScroll", parent);
            var scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.sprite = rounded;
            scrollImage.type = Image.Type.Sliced;
            scrollImage.color = UITheme.FieldBackground;
            Element(scrollGo, prefHeight: 340f);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = NewUi("Viewport", scrollGo.transform);
            Stretch(viewport, 6f);
            viewport.AddComponent<RectMask2D>();
            viewport.AddComponent<Image>().color = Color.clear;

            var text = Text(viewport.transform, "Text", "", 12f, UITheme.Text, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = new Vector2(4f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = Scrollbar(scrollGo.transform);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = textRect;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return text;
        }

        // ── Shared pieces ─────────────────────────────────────────────────────

        /// <summary>
        /// Own canvas per panel: when the inspector values change, only that
        /// panel is re-batched instead of the whole UI (library list included).
        /// Every nested canvas needs its own raycaster to receive clicks.
        /// </summary>
        private static void SubCanvas(GameObject go)
        {
            go.AddComponent<Canvas>();
            go.AddComponent<GraphicRaycaster>();
        }

        private static void TabBar(Transform panel, string titleKey)
        {
            var bar = NewUi("TabBar", panel);
            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -TabHeight);
            rect.offsetMax = Vector2.zero;
            bar.AddComponent<Image>().color = UITheme.WindowTab;

            // Active tab, as in the Unity window header
            var tab = NewUi("Tab", bar.transform);
            var tabRect = tab.GetComponent<RectTransform>();
            tabRect.anchorMin = new Vector2(0f, 0f);
            tabRect.anchorMax = new Vector2(0f, 1f);
            tabRect.pivot = new Vector2(0f, 0.5f);
            tabRect.offsetMin = new Vector2(0f, 0f);
            tabRect.offsetMax = new Vector2(130f, -3f);
            tab.AddComponent<Image>().color = UITheme.PanelBackground;

            var label = Localize(Text(tab.transform, "Title", "", 12f, UITheme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold), titleKey);
            Stretch(label.gameObject);
            label.margin = new Vector4(10f, 0f, 6f, 0f);
        }

        /// <summary>Foldout section with a header row; returns the body to put rows into.</summary>
        private static Transform Foldout(Transform parent, string titleKey, out GameObject header)
        {
            var root = NewUi(titleKey + "_Section", parent);
            var layout = root.AddComponent<VerticalLayoutGroup>();
            SetControl(layout, true, true, true, false);

            header = NewUi("Header", root.transform);
            var headerImage = header.AddComponent<Image>();
            var button = header.AddComponent<Button>();
            StyleButton(button, headerImage, UITheme.SectionHeader, UITheme.Hover, UITheme.ButtonPressed);
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(8, 8, 0, 0);
            headerLayout.spacing = 6f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            SetControl(headerLayout, true, true, false, false);
            Element(header, minHeight: 24f);

            var arrow = Arrow(header.transform);
            var label = Localize(Text(header.transform, "Title", "", 12f, UITheme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold), titleKey);
            Element(label.gameObject, flexWidth: 1f);

            var body = NewUi("Body", root.transform);
            var bodyLayout = body.AddComponent<VerticalLayoutGroup>();
            bodyLayout.padding = new RectOffset(10, 10, 6, 10);
            bodyLayout.spacing = 4f;
            SetControl(bodyLayout, true, true, true, false);

            Divider(root.transform);

            var foldout = root.AddComponent<FoldoutSection>();
            var so = new SerializedObject(foldout);
            so.FindProperty("headerButton").objectReferenceValue = button;
            so.FindProperty("arrow").objectReferenceValue = arrow;
            so.FindProperty("content").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();

            return body.transform;
        }

        private static void Divider(Transform parent)
        {
            var line = NewUi("Divider", parent);
            line.AddComponent<Image>().color = UITheme.Divider;
            Element(line, minHeight: 1f, prefHeight: 1f);
        }

        private static RectTransform Arrow(Transform parent)
        {
            var go = NewUi("Arrow", parent);
            var image = go.AddComponent<Image>();
            image.sprite = arrowSprite;
            image.color = UITheme.TextDim;
            image.raycastTarget = false;
            Element(go, prefWidth: 10f, prefHeight: 10f);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>Small tab on the outer edge of a side panel that hides / shows it.</summary>
        private static CollapsibleSidebar SidebarTab(GameObject panel, CollapsibleSidebar.Side side)
        {
            var tab = NewUi("ToggleTab", panel.transform);
            var rect = tab.GetComponent<RectTransform>();
            var left = side == CollapsibleSidebar.Side.Left;
            rect.anchorMin = rect.anchorMax = new Vector2(left ? 1f : 0f, 0.5f);
            rect.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(16f, 56f);

            var image = tab.AddComponent<Image>();
            var button = tab.AddComponent<Button>();
            StyleButton(button, image, UITheme.PanelBackground, UITheme.Hover, UITheme.ButtonPressed);

            var arrow = NewUi("Arrow", tab.transform);
            var arrowRect = arrow.GetComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(10f, 10f);
            var arrowImage = arrow.AddComponent<Image>();
            arrowImage.sprite = arrowSprite;
            arrowImage.color = UITheme.Text;
            arrowImage.raycastTarget = false;

            var sidebar = panel.AddComponent<CollapsibleSidebar>();
            var so = new SerializedObject(sidebar);
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<RectTransform>();
            so.FindProperty("toggleButton").objectReferenceValue = button;
            so.FindProperty("toggleArrow").objectReferenceValue = arrowRect;
            so.FindProperty("side").enumValueIndex = (int)side;
            so.ApplyModifiedPropertiesWithoutUndo();
            return sidebar;
        }

        private static Button TextButton(Transform parent, string name, string label, float fontSize)
        {
            var go = NewUi(name, parent);
            var image = go.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            var button = go.AddComponent<Button>();
            StyleButton(button, image, UITheme.Button, UITheme.ButtonHover, UITheme.ButtonPressed);

            var text = Text(go.transform, "Text", label, fontSize, UITheme.Text, TextAlignmentOptions.Center);
            Stretch(text.gameObject);
            return button;
        }

        private static Button SmallButton(Transform parent, string labelKey, float width)
        {
            var button = LocalizeButton(TextButton(parent, labelKey + "_Button", "", 10f), labelKey);
            Element(button.gameObject, prefWidth: width, prefHeight: 18f);
            return button;
        }

        private static TMP_InputField InputField(Transform parent, string name, string placeholder,
            TextAlignmentOptions alignment, float fontSize)
        {
            var go = NewUi(name, parent);
            var image = go.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = UITheme.FieldBackground;

            var area = NewUi("Text Area", go.transform);
            Stretch(area);
            var areaRect = area.GetComponent<RectTransform>();
            areaRect.offsetMin = new Vector2(5f, 1f);
            areaRect.offsetMax = new Vector2(-5f, -1f);
            area.AddComponent<RectMask2D>().padding = new Vector4(-4f, 0f, -4f, 0f);

            var hint = Text(area.transform, "Placeholder", placeholder, fontSize, UITheme.TextDim, alignment, FontStyles.Italic);
            Stretch(hint.gameObject);
            var text = Text(area.transform, "Text", "", fontSize, Color.white, alignment);
            Stretch(text.gameObject);

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = hint;
            input.fontAsset = font;
            input.pointSize = fontSize;
            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.selectionColor = UITheme.Accent;
            input.targetGraphic = image;
            var colors = input.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.colorMultiplier = 1f;
            input.colors = colors;

            go.AddComponent<InspectorInputFocus>();
            return input;
        }

        private static Scrollbar Scrollbar(Transform parent)
        {
            var go = NewUi("Scrollbar", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(6f, 0f);
            go.AddComponent<Image>().color = Color.clear;

            var area = NewUi("Sliding Area", go.transform);
            Stretch(area);

            var handle = NewUi("Handle", area.transform);
            Stretch(handle);
            var handleImage = handle.AddComponent<Image>();
            handleImage.sprite = rounded;
            handleImage.type = Image.Type.Sliced;

            var scrollbar = go.AddComponent<Scrollbar>();
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            StyleButtonColors(scrollbar, handleImage, UITheme.Button, UITheme.ButtonHover, UITheme.ButtonHover);
            return scrollbar;
        }

        // ── Localization ──────────────────────────────────────────────────────

        /// <summary>
        /// Takes the text from the language files (Assets/StreamingAssets/Localization)
        /// by <paramref name="key"/> and keeps it updated when the language changes.
        /// </summary>
        private static T Localize<T>(T text, string key, string prefix = null, string suffix = null) where T : TMP_Text
        {
            text.gameObject.AddComponent<LocalizedText>().Setup(key, prefix, suffix);
            return text;
        }

        private static Button LocalizeButton(Button button, string key, string prefix = null, string suffix = null)
        {
            Localize(button.GetComponentInChildren<TMP_Text>(), key, prefix, suffix);
            return button;
        }

        // ── Low-level helpers ─────────────────────────────────────────────────

        private static GameObject NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
        {
            var go = NewUi(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(GameObject go, float inset = 0f)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static LayoutElement Element(GameObject go, float minHeight = -1f, float prefWidth = -1f,
            float prefHeight = -1f, float flexWidth = -1f)
        {
            // No "??" here: in the editor a missing component is a fake-null object.
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();
            element.minHeight = minHeight;
            element.preferredWidth = prefWidth;
            element.preferredHeight = prefHeight;
            element.flexibleWidth = flexWidth;
            if (prefWidth >= 0f)
                element.minWidth = prefWidth;
            return element;
        }

        private static void SetControl(HorizontalOrVerticalLayoutGroup layout, bool controlWidth, bool controlHeight,
            bool expandWidth, bool expandHeight)
        {
            layout.childControlWidth = controlWidth;
            layout.childControlHeight = controlHeight;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = expandHeight;
        }

        /// <summary>White graphic + real colours in the ColorBlock, so hover colours are exact.</summary>
        private static void StyleButton(Selectable selectable, Image image, Color normal, Color hover, Color pressed)
        {
            image.color = Color.white;
            if (image.sprite == null)
            {
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
            }
            StyleButtonColors(selectable, image, normal, hover, pressed);
        }

        private static void StyleButtonColors(Selectable selectable, Graphic graphic, Color normal, Color hover, Color pressed)
        {
            selectable.targetGraphic = graphic;
            var colors = selectable.colors;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.pressedColor = pressed;
            colors.selectedColor = normal;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;

            // Clicking must not leave the button "selected" (that would also eat keyboard focus).
            var nav = selectable.navigation;
            nav.mode = Navigation.Mode.None;
            selectable.navigation = nav;
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            var path = $"{PrefabFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
