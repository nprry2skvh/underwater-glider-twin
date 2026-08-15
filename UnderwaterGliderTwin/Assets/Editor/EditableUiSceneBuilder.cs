using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnderwaterGliderTwin.Bootstrap;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Editor
{
    public static class EditableUiSceneBuilder
    {
        private const string WelcomeScenePath = "Assets/Scenes/Welcome.unity";
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string PrefabFolderPath = "Assets/UI/Prefabs";

        [MenuItem("UnderwaterGliderTwin/UI/Rebuild Welcome UI")]
        public static void BuildWelcomeScene()
        {
            EnsureNoUnsavedSceneChanges(WelcomeScenePath, "Welcome UI rebuild");
            var scene = EditorSceneManager.OpenScene(WelcomeScenePath);
            var bootstrapObject = FindSceneObject(scene, "WelcomeBootstrap") ?? new GameObject("WelcomeBootstrap");
            if (bootstrapObject.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(bootstrapObject, scene);
            }

            var bootstrap = bootstrapObject.GetComponent<WelcomeBootstrap>() ?? bootstrapObject.AddComponent<WelcomeBootstrap>();
            var canvasObject = FindSceneObject(scene, "WelcomeCanvas");
            var canvasWasCreated = canvasObject == null;
            if (canvasWasCreated)
            {
                canvasObject = new GameObject("WelcomeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
            }

            var canvas = canvasObject.GetComponent<Canvas>() ?? canvasObject.AddComponent<Canvas>();
            if (canvasWasCreated)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 0;
            }

            var scaler = canvasObject.GetComponent<CanvasScaler>() ?? canvasObject.AddComponent<CanvasScaler>();
            if (canvasWasCreated)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            EnsureEventSystem(scene);
            var refs = CreateWelcomeChildren(canvasObject.transform);
            AssignWelcomeReferences(bootstrap, canvas, refs);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("UnderwaterGliderTwin/UI/Reset Welcome UI Defaults")]
        public static void ResetWelcomeDefaults()
        {
            EnsureNoUnsavedSceneChanges(WelcomeScenePath, "Welcome UI reset");
            var scene = EditorSceneManager.OpenScene(WelcomeScenePath);
            var canvas = FindSceneObject(scene, "WelcomeCanvas");
            if (canvas == null)
            {
                BuildWelcomeScene();
                return;
            }

            ResetImage(canvas.transform.Find("BackgroundImage"), Vector2.zero, Vector2.one, new Color(0.025f, 0.12f, 0.18f));
            var panel = canvas.transform.Find("LaunchPanel");
            ResetImage(panel, new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), new Color(0.03f, 0.12f, 0.22f, 0.98f));
            ResetText(panel, "TitleText", "Underwater Glider Digital Twin", 34, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f));
            ResetText(panel, "DescriptionText", "CSV replay, simulation, and short-horizon prediction", 18, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.83f));
            ResetInput(panel, "CsvPathInput", new Vector2(0.08f, 0.58f), new Vector2(0.72f, 0.67f));
            ResetButton(panel, "ConfirmCsvButton", "Confirm CSV Path", new Vector2(0.74f, 0.58f), new Vector2(0.92f, 0.67f));
            ResetButton(panel, "StartCsvButton", "Start CSV Replay", new Vector2(0.08f, 0.43f), new Vector2(0.48f, 0.53f));
            ResetButton(panel, "SimulationButton", "Enter Simulation", new Vector2(0.52f, 0.43f), new Vector2(0.92f, 0.53f));
            ResetText(panel, "LaunchStatusText", string.Empty, 15, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.30f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("UnderwaterGliderTwin/UI/Rebuild Main UI")]
        public static void BuildMainScene()
        {
            EnsureNoUnsavedSceneChanges(MainScenePath, "Main UI rebuild");
            EnsurePrefabFolder();
            var scene = EditorSceneManager.OpenScene(MainScenePath);
            var rootObject = FindSceneObject(scene, "RuntimeUiRoot");
            var rootWasCreated = rootObject == null;
            if (rootWasCreated)
            {
                rootObject = new GameObject("RuntimeUiRoot");
                SceneManager.MoveGameObjectToScene(rootObject, scene);
            }

            var runtimeRoot = rootObject.GetComponent<RuntimeUiRoot>() ?? rootObject.AddComponent<RuntimeUiRoot>();
            var canvasObject = FindDirectChild(rootObject.transform, "RuntimeCanvas");
            var canvasWasCreated = canvasObject == null;
            if (canvasWasCreated)
            {
                canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(rootObject.transform, false);
            }

            var canvas = canvasObject.GetComponent<Canvas>() ?? canvasObject.AddComponent<Canvas>();
            if (canvasWasCreated)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 0;
            }

            var scaler = canvasObject.GetComponent<CanvasScaler>() ?? canvasObject.AddComponent<CanvasScaler>();
            if (canvasWasCreated)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            EnsureSceneChild(canvas.transform, "CommandCenterHeader", typeof(RectTransform), typeof(Image), out var headerWasCreated);
            if (headerWasCreated)
            {
                ConfigureRect(canvas.transform.Find("CommandCenterHeader") as RectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 48f));
                canvas.transform.Find("CommandCenterHeader").GetComponent<Image>().color = new Color(0.015f, 0.09f, 0.14f, 0.98f);
            }

            var panelNames = new[]
            {
                "DashboardPanel",
                "StatusPanel",
                "DataInputPanel",
                "PlaybackControlsPanel",
                "OceanCommandToolbar"
            };
            foreach (var panelName in panelNames)
            {
                EnsurePanelPrefabInstance(scene, canvas.transform, panelName);
            }

            var modalRoot = FindDirectChild(canvas.transform, "ModalRoot");
            if (modalRoot == null)
            {
                modalRoot = new GameObject("ModalRoot", typeof(RectTransform));
                modalRoot.transform.SetParent(canvas.transform, false);
                ConfigureRect(modalRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }

            EnsureEventSystem(scene);
            AssignMainReferences(runtimeRoot, canvas, modalRoot.GetComponent<RectTransform>(), rootWasCreated);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("UnderwaterGliderTwin/UI/Reset Main UI Defaults")]
        public static void ResetMainUiDefaults()
        {
            EnsureNoUnsavedSceneChanges(MainScenePath, "Main UI reset");
            var scene = EditorSceneManager.OpenScene(MainScenePath);
            var panelNames = new[]
            {
                "DashboardPanel",
                "StatusPanel",
                "DataInputPanel",
                "PlaybackControlsPanel",
                "OceanCommandToolbar"
            };
            foreach (var panelName in panelNames)
            {
                var keep = false;
                foreach (var panel in FindSceneObjects(scene, panelName))
                {
                    if (!keep && PrefabUtility.GetPrefabInstanceStatus(panel) == PrefabInstanceStatus.Connected)
                    {
                        keep = true;
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(panel);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AssignMainReferences(RuntimeUiRoot runtimeRoot, Canvas canvas, RectTransform modalRoot, bool rootWasCreated)
        {
            var serialized = new SerializedObject(runtimeRoot);
            serialized.FindProperty("runtimeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            if (rootWasCreated)
            {
                serialized.FindProperty("enabledPanelValidationMask").intValue = (int)RuntimeUiPanelFlags.None;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runtimeRoot);
        }

        private static void EnsurePanelPrefabInstance(Scene scene, Transform runtimeCanvas, string panelName)
        {
            var prefabPath = PrefabFolderPath + "/" + panelName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                prefab = CreatePanelPrefab(prefabPath, panelName);
            }

            var existing = default(GameObject);
            foreach (var candidate in FindSceneObjects(scene, panelName))
            {
                var status = PrefabUtility.GetPrefabInstanceStatus(candidate);
                if (status != PrefabInstanceStatus.Connected)
                {
                    throw new InvalidOperationException(
                        "Main UI contains a same-name non-Prefab panel: " +
                        GetTransformPath(candidate.transform) + " (" + panelName + ")");
                }

                if (existing != null)
                {
                    throw new InvalidOperationException("Main UI contains duplicate connected Prefab panels: " + panelName);
                }

                existing = candidate;
            }

            if (existing != null)
            {
                if (existing.transform.parent != runtimeCanvas)
                {
                    existing.transform.SetParent(runtimeCanvas, false);
                }

                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate UI Prefab: " + prefabPath);
            }

            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetParent(runtimeCanvas, false);
            ConfigureNewPanelLayout(instance.transform as RectTransform, panelName);
        }

        private static GameObject CreatePanelPrefab(string prefabPath, string panelName)
        {
            var temporary = new GameObject(panelName, typeof(RectTransform), typeof(Image));
            var image = temporary.GetComponent<Image>();
            image.color = panelName == "PlaybackControlsPanel"
                ? new Color(0.015f, 0.09f, 0.14f, 0.98f)
                : new Color(0.025f, 0.12f, 0.18f, 0.92f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/UI"))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolderPath))
            {
                AssetDatabase.CreateFolder("Assets/UI", "Prefabs");
            }
        }

        private static void ConfigureNewPanelLayout(RectTransform rect, string panelName)
        {
            if (rect == null)
            {
                return;
            }

            switch (panelName)
            {
                case "DashboardPanel":
                    ConfigureRect(rect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-48f, -208f));
                    break;
                case "StatusPanel":
                    ConfigureRect(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(-48f, 96f));
                    break;
                case "DataInputPanel":
                    ConfigureRect(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 158f), new Vector2(-48f, 48f));
                    break;
                case "PlaybackControlsPanel":
                    ConfigureRect(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(-48f, 124f));
                    break;
                case "OceanCommandToolbar":
                    ConfigureRect(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -112f), new Vector2(360f, 48f));
                    break;
            }
        }

        private static GameObject EnsureSceneChild(Transform parent, string name, Type firstComponent, Type secondComponent, out bool wasCreated)
        {
            var existing = FindDirectChild(parent, name);
            if (existing != null)
            {
                wasCreated = false;
                return existing;
            }

            wasCreated = true;
            var created = new GameObject(name, firstComponent, secondComponent);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static GameObject FindDirectChild(Transform parent, string name)
        {
            return parent == null ? null : parent.Find(name)?.gameObject;
        }

        private static void EnsureNoUnsavedSceneChanges(string targetScenePath, string operationName)
        {
            foreach (var openScene in GetOpenScenes())
            {
                if (openScene.isDirty && (openScene.path == targetScenePath || openScene == EditorSceneManager.GetActiveScene()))
                {
                    throw new InvalidOperationException(operationName + " cancelled because scene has unsaved changes: " + openScene.path);
                }
            }

            if (Application.isBatchMode && EditorSceneManager.GetActiveScene().isDirty)
            {
                throw new InvalidOperationException(operationName + " cancelled because the active scene has unsaved changes. Save or revert the scene before running the batch builder.");
            }

            if (Application.isBatchMode)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new InvalidOperationException(operationName + " cancelled because there are unsaved scene changes.");
            }
        }

        private static IEnumerable<Scene> GetOpenScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                yield return SceneManager.GetSceneAt(index);
            }
        }

        private static void EnsureEventSystem(Scene scene)
        {
            foreach (var eventSystem in UnityEngine.Object.FindObjectsOfType<EventSystem>(true))
            {
                if (eventSystem.gameObject.scene == scene)
                {
                    return;
                }
            }

            var created = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(created, scene);
        }

        private readonly struct WelcomeUiBuildRefs
        {
            public WelcomeUiBuildRefs(InputField csvInput, Text status, Button confirmCsvButton, Button startCsvButton, Button simulationButton)
            {
                CsvInput = csvInput;
                Status = status;
                ConfirmCsvButton = confirmCsvButton;
                StartCsvButton = startCsvButton;
                SimulationButton = simulationButton;
            }

            public InputField CsvInput { get; }
            public Text Status { get; }
            public Button ConfirmCsvButton { get; }
            public Button StartCsvButton { get; }
            public Button SimulationButton { get; }
        }

        private static WelcomeUiBuildRefs CreateWelcomeChildren(Transform canvas)
        {
            GetOrCreateImage(canvas, "BackgroundImage", Vector2.zero, Vector2.one, new Color(0.025f, 0.12f, 0.18f));
            var panel = GetOrCreateImage(canvas, "LaunchPanel", new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), new Color(0.03f, 0.12f, 0.22f, 0.98f));
            GetOrCreateText(panel.transform, "TitleText", "Underwater Glider Digital Twin", 34, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f));
            GetOrCreateText(panel.transform, "DescriptionText", "CSV replay, simulation, and short-horizon prediction", 18, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.83f));
            var csvInput = GetOrCreateInput(panel.transform, "CsvPathInput", new Vector2(0.08f, 0.58f), new Vector2(0.72f, 0.67f));
            var confirm = GetOrCreateButton(panel.transform, "ConfirmCsvButton", "Confirm CSV Path", new Vector2(0.74f, 0.58f), new Vector2(0.92f, 0.67f));
            var start = GetOrCreateButton(panel.transform, "StartCsvButton", "Start CSV Replay", new Vector2(0.08f, 0.43f), new Vector2(0.48f, 0.53f));
            var simulation = GetOrCreateButton(panel.transform, "SimulationButton", "Enter Simulation", new Vector2(0.52f, 0.43f), new Vector2(0.92f, 0.53f));
            var status = GetOrCreateText(panel.transform, "LaunchStatusText", string.Empty, 15, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.30f));
            return new WelcomeUiBuildRefs(csvInput, status, confirm, start, simulation);
        }

        private static void AssignWelcomeReferences(WelcomeBootstrap bootstrap, Canvas canvas, WelcomeUiBuildRefs refs)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("welcomeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("csvInput").objectReferenceValue = refs.CsvInput;
            serialized.FindProperty("status").objectReferenceValue = refs.Status;
            serialized.FindProperty("confirmCsvButton").objectReferenceValue = refs.ConfirmCsvButton;
            serialized.FindProperty("startCsvButton").objectReferenceValue = refs.StartCsvButton;
            serialized.FindProperty("simulationButton").objectReferenceValue = refs.SimulationButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        private static Image GetOrCreateImage(Transform parent, string name, Vector2 min, Vector2 max, Color defaultColor)
        {
            var go = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(Image), out var isNew);
            var rect = go.GetComponent<RectTransform>();
            if (isNew)
            {
                ConfigureRect(rect, min, max);
            }

            var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            if (isNew)
            {
                image.color = defaultColor;
            }

            return image;
        }

        private static Text GetOrCreateText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
        {
            var go = GetOrCreateChild(parent, name, typeof(RectTransform), typeof(Text), out var isNew);
            var rect = go.GetComponent<RectTransform>();
            if (isNew)
            {
                ConfigureRect(rect, min, max);
            }

            var text = go.GetComponent<Text>() ?? go.AddComponent<Text>();
            if (isNew)
            {
                text.text = value;
                text.fontSize = size;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleLeft;
            }

            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return text;
        }

        private static Button GetOrCreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            var image = GetOrCreateImage(parent, name, min, max, new Color(0.04f, 0.48f, 0.58f));
            var button = image.GetComponent<Button>();
            if (button == null)
            {
                button = image.gameObject.AddComponent<Button>();
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = image;
            }

            var labelWasMissing = button.transform.Find(name + "Label") == null;
            var text = GetOrCreateText(button.transform, name + "Label", label, 16, Vector2.zero, Vector2.one);
            if (labelWasMissing)
            {
                text.alignment = TextAnchor.MiddleCenter;
            }
            return button;
        }

        private static InputField GetOrCreateInput(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var image = GetOrCreateImage(parent, name, min, max, new Color(0.02f, 0.10f, 0.14f));
            var input = image.GetComponent<InputField>();
            if (input == null)
            {
                input = image.gameObject.AddComponent<InputField>();
            }

            var text = GetOrCreateText(input.transform, "Text", string.Empty, 15, new Vector2(.03f, 0), new Vector2(.97f, 1));
            if (input.textComponent == null)
            {
                input.textComponent = text;
            }

            var placeholder = GetOrCreateText(input.transform, "Placeholder", "Enter CSV absolute path", 15, new Vector2(.03f, 0), new Vector2(.97f, 1));
            if (input.placeholder == null)
            {
                placeholder.color = new Color(.6f, .7f, .72f);
                input.placeholder = placeholder;
            }

            return input;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name, Type firstComponent, Type secondComponent, out bool isNew)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                isNew = false;
                return existing.gameObject;
            }

            isNew = true;
            var go = new GameObject(name, firstComponent, secondComponent);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void ConfigureRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ConfigureRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                    {
                        return transform.gameObject;
                    }
                }
            }

            return null;
        }

        private static List<GameObject> FindSceneObjects(Scene scene, string name)
        {
            var matches = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                    {
                        matches.Add(transform.gameObject);
                    }
                }
            }

            return matches;
        }

        private static void ResetImage(Transform target, Vector2 min, Vector2 max, Color color)
        {
            if (target == null)
            {
                return;
            }

            ConfigureRect(target as RectTransform, min, max);
            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private static void ResetText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
        {
            var text = GetOrCreateText(parent, name, value, size, min, max);
            ConfigureRect(text.rectTransform, min, max);
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
        }

        private static void ResetButton(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            var button = GetOrCreateButton(parent, name, label, min, max);
            ConfigureRect(button.transform as RectTransform, min, max);
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.04f, 0.48f, 0.58f);
            }

            var labelText = button.transform.Find(name + "Label")?.GetComponent<Text>();
            if (labelText != null)
            {
                labelText.text = label;
                labelText.fontSize = 16;
                labelText.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void ResetInput(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var input = GetOrCreateInput(parent, name, min, max);
            ConfigureRect(input.transform as RectTransform, min, max);
            input.text = string.Empty;
        }
    }
}
