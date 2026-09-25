using System.IO;
using UnityEditor;
using UnityEngine;

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
            var database = LoadOrCreateDatabase();

            ConfigureGroundPlane();
            ConfigureCamera();
            var managers = CreateManagers(previewMaterial);
            var canvas = PlacementUIBuilder.Build(managers, database);

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

        /// <summary>
        /// Uses the existing equipment database. Sample furniture data is only
        /// generated for an empty project — never over a real database.
        /// </summary>
        private static PlacementAssetDatabase LoadOrCreateDatabase()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlacementAssetDatabase>(DataFolder + "/PlacementAssetDatabase.asset");
            if (existing != null)
                return existing;

            return CreateSampleDatabase(CreateSamplePrefabs());
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
