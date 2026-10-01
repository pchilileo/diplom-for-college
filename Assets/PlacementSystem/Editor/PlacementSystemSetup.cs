using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
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

            WireManagers(managers, canvas);

            EditorUtility.DisplayDialog(
                "Placement System",
                "Сцена настроена.\n\n" +
                "Управление и все функции описаны во встроенной справке: Ctrl+O в режиме Play.",
                "OK");
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

        /// <summary>Uses the existing equipment database or creates an empty one.</summary>
        private static PlacementAssetDatabase LoadOrCreateDatabase()
        {
            var path = DataFolder + "/PlacementAssetDatabase.asset";
            var database = AssetDatabase.LoadAssetAtPath<PlacementAssetDatabase>(path);
            if (database != null)
                return database;

            database = ScriptableObject.CreateInstance<PlacementAssetDatabase>();
            AssetDatabase.CreateAsset(database, path);
            AssetDatabase.SaveAssets();
            return database;
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

            // Editor modes: 1 objects, 2 connect wires, 3 delete wires
            if (root.GetComponent<WireConnectionMode>() == null)
                root.AddComponent<WireConnectionMode>();

            if (root.GetComponent<WireDeleteMode>() == null)
                root.AddComponent<WireDeleteMode>();

            if (root.GetComponent<EditorModeManager>() == null)
                root.AddComponent<EditorModeManager>();

            var placementSo = new SerializedObject(root.GetComponent<PlacementManager>());
            placementSo.FindProperty("previewMaterial").objectReferenceValue = previewMaterial;
            placementSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void WireManagers(GameObject managers, GameObject canvas)
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

            EditorUtility.SetDirty(managers);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
