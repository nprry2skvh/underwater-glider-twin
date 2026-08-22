using System;
using System.Collections.Generic;
using System.Reflection;
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

            var uiRoot = EnsureLayoutContainer(canvas.transform, "UiRoot");
            ConfigureRect(uiRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var systemBar = EnsureLayoutContainer(uiRoot, "SystemBar");
            ConfigureRect(systemBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 48f));
            MoveLegacyChildToContainer(canvas.transform, "CommandCenterHeader", systemBar);
            var configurationArea = EnsureLayoutContainer(uiRoot, "ConfigurationArea");
            ConfigureRect(configurationArea, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 320f));
            var playbackBar = EnsureLayoutContainer(uiRoot, "PlaybackBar");
            ConfigureRect(playbackBar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 320f), new Vector2(0f, 124f));
            var mainBody = EnsureLayoutContainer(uiRoot, "MainBody");
            ConfigureRect(mainBody, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(0f, -492f));
            var telemetryColumn = EnsureLayoutContainer(mainBody, "TelemetryColumn");
            ConfigureRect(telemetryColumn, new Vector2(0f, 0f), new Vector2(0.25f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var viewportColumn = EnsureLayoutContainer(mainBody, "ViewportColumn");
            ConfigureRect(viewportColumn, new Vector2(0.25f, 0f), new Vector2(0.75f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var statusColumn = EnsureLayoutContainer(mainBody, "StatusColumn");
            ConfigureRect(statusColumn, new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            var drawerEntryLayer = EnsureLayoutContainer(uiRoot, "DrawerEntryLayer");
            ConfigureRect(drawerEntryLayer, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            drawerEntryLayer.SetAsLastSibling();
            EnsureDrawerEntryToggle(drawerEntryLayer, "TelemetryDrawerToggle", "遥测抽屉", new Vector2(16f, -12f), new Vector2(120f, UiFactory.MinimumDrawerToggleHeight));
            EnsureDrawerEntryToggle(drawerEntryLayer, "StatusDrawerToggle", "状态抽屉", new Vector2(148f, -12f), new Vector2(120f, UiFactory.MinimumDrawerToggleHeight));

            EnsurePanelPrefabInstance(scene, telemetryColumn, "DashboardPanel");
            EnsurePanelPrefabInstance(scene, statusColumn, "StatusPanel");
            EnsurePanelPrefabInstance(scene, configurationArea, "DataInputPanel");
            EnsurePanelPrefabInstance(scene, playbackBar, "PlaybackControlsPanel");
            EnsurePanelPrefabInstance(scene, viewportColumn, "OceanCommandToolbar");

            var modalRoot = FindDirectChild(canvas.transform, "ModalRoot");
            if (modalRoot == null)
            {
                modalRoot = new GameObject("ModalRoot", typeof(RectTransform));
                modalRoot.transform.SetParent(canvas.transform, false);
            }
            ConfigureRect(modalRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var drawerScrim = GetOrCreateImage(modalRoot.transform, "DrawerScrim", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            drawerScrim.raycastTarget = false;
            drawerScrim.transform.SetAsFirstSibling();

            EnsureModalPrefabInstance(scene, modalRoot.transform, "OceanCurrentDrawer");
            EnsureModalPrefabInstance(scene, modalRoot.transform, "FlightLegDrawer");

            EnsureEventSystem(scene);
            var responsiveController = uiRoot.GetComponent<ResponsiveUiLayoutController>() ?? uiRoot.gameObject.AddComponent<ResponsiveUiLayoutController>();
            EditorUtility.SetDirty(responsiveController);
            AssignMainReferences(runtimeRoot, canvas, uiRoot, modalRoot.GetComponent<RectTransform>());
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

        private static void AssignMainReferences(RuntimeUiRoot runtimeRoot, Canvas canvas, RectTransform uiRoot, RectTransform modalRoot)
        {
            var serialized = new SerializedObject(runtimeRoot);
            serialized.FindProperty("runtimeCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            serialized.FindProperty("enabledPanelValidationMask").intValue = (int)RuntimeUiPanelFlags.All;

            var dashboard = FindDescendant(canvas.transform, "DashboardPanel");
            var status = FindDescendant(canvas.transform, "StatusPanel");
            var dataInput = FindDescendant(canvas.transform, "DataInputPanel");
            var playback = FindDescendant(canvas.transform, "PlaybackControlsPanel");
            var oceanToolbar = FindDescendant(canvas.transform, "OceanCommandToolbar");
            AssignUiGroup(serialized, new ResponsiveLayoutRefs(), "references.layout", uiRoot, modalRoot);
            AssignUiGroup(serialized, new DashboardPanelRefs(), "references.dashboard", dashboard != null ? dashboard.transform : null, modalRoot);
            AssignUiGroup(serialized, new StatusPanelRefs(), "references.status", status != null ? status.transform : null, modalRoot);
            AssignUiGroup(serialized, new DataInputPanelRefs(), "references.dataInput", dataInput != null ? dataInput.transform : null, modalRoot);
            AssignUiGroup(serialized, new PlaybackControlsRefs(), "references.playback", playback != null ? playback.transform : null, modalRoot);
            AssignUiGroup(serialized, new OceanToolbarRefs(), "references.oceanToolbar", oceanToolbar != null ? oceanToolbar.transform : null, modalRoot);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runtimeRoot);

            var bootstrapObject = FindSceneObject(runtimeRoot.gameObject.scene, "TwinBootstrap");
            var twinBootstrap = bootstrapObject != null ? bootstrapObject.GetComponent<TwinBootstrap>() : null;
            if (twinBootstrap != null)
            {
                var bootstrapSerialized = new SerializedObject(twinBootstrap);
                bootstrapSerialized.FindProperty("runtimeUiRoot").objectReferenceValue = runtimeRoot;
                bootstrapSerialized.FindProperty("useGeneratedRuntimeUi").boolValue = false;
                bootstrapSerialized.FindProperty("allowRuntimeFallback").boolValue = false;
                bootstrapSerialized.FindProperty("strictUiValidation").boolValue = true;
                bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(twinBootstrap);
            }
        }

        private static void AssignUiGroup(SerializedObject serialized, object group, string groupPath, Transform panelRoot, RectTransform modalRoot)
        {
            foreach (var field in group.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var property = serialized.FindProperty(groupPath + "." + field.Name);
                if (property == null)
                {
                    continue;
                }

                if (typeof(IUiReferenceGroup).IsAssignableFrom(field.FieldType))
                {
                    var nested = field.GetValue(group);
                    if (nested != null)
                    {
                        AssignUiGroup(serialized, nested, groupPath + "." + field.Name, panelRoot, modalRoot);
                    }

                    continue;
                }

                if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                var targetRoot = ResolveReferenceRoot(groupPath, field.Name, panelRoot, modalRoot);
                UnityEngine.Object value = null;
                if (field.Name == "panel")
                {
                    value = panelRoot as RectTransform;
                }
                else if (targetRoot != null)
                {
                    if (groupPath == "references.layout" && field.Name == "drawerScrim")
                    {
                        value = FindDirectChild(modalRoot, "DrawerScrim")?.GetComponent(field.FieldType);
                    }
                    else if (groupPath == "references.dataInput.ocean" && field.Name == "oceanCurrentDrawer")
                    {
                        value = FindDirectChild(modalRoot, "OceanCurrentDrawer")?.GetComponent(field.FieldType);
                    }
                    else if (groupPath == "references.dataInput.flightLeg" && field.Name == "drawer")
                    {
                        value = FindDirectChild(modalRoot, "FlightLegDrawer")?.GetComponent(field.FieldType);
                    }
                    else
                    {
                        var objectName = GetUiObjectName(field.Name);
                        var target = FindDescendant(targetRoot, objectName);
                        if (target != null)
                        {
                            value = field.FieldType == typeof(GameObject)
                                ? target
                                : target.GetComponent(field.FieldType);
                        }
                    }
                }

                property.objectReferenceValue = value;
            }
        }

        private static Transform ResolveReferenceRoot(string groupPath, string fieldName, Transform panelRoot, RectTransform modalRoot)
        {
            if (groupPath == "references.layout")
            {
                return fieldName == "drawerScrim" ? modalRoot : panelRoot;
            }

            if (groupPath == "references.dataInput.dynamics"
                || groupPath == "references.dataInput.flightLeg"
                || (groupPath == "references.dataInput.ocean" && ShouldLiveInModal("dataInput.ocean", fieldName)))
            {
                if (modalRoot == null)
                {
                    return null;
                }

                if (groupPath == "references.dataInput.flightLeg")
                {
                    return FindDirectChild(modalRoot, "FlightLegDrawer")?.transform;
                }

                return FindDirectChild(modalRoot, "OceanCurrentDrawer")?.transform;
            }

            return panelRoot;
        }

        private static string GetUiObjectName(string fieldName)
        {
            switch (fieldName)
            {
                case "dynamicRowsRoot": return "DynamicRowsRoot";
                case "oceanLayerRowTemplate": return "OceanCurrentLayerRowTemplate";
                case "predictionMetricRowsRoot": return "PredictionMetricRowsRoot";
                case "predictionMetricRowTemplate": return "PredictionMetricRowTemplate";
                case "advancedRowsRoot": return "AdvancedRowsRoot";
                default: return char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1);
            }
        }

        private static GameObject FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static void EnsurePanelPrefabInstance(Scene scene, Transform runtimeCanvas, string panelName)
        {
            var prefabPath = PrefabFolderPath + "/" + panelName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                prefab = CreatePanelPrefab(prefabPath, panelName);
            }
            else if (prefab.transform.childCount == 0)
            {
                PopulateExistingPanelPrefab(prefabPath, panelName);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
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

                MigrateLegacyPanelLayout(existing.transform as RectTransform, panelName);

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

        private static void EnsureModalPrefabInstance(Scene scene, Transform modalRoot, string drawerName)
        {
            var prefabPath = PrefabFolderPath + "/" + drawerName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                prefab = CreateModalPrefab(prefabPath, drawerName);
            }
            else if (prefab.transform.childCount == 0)
            {
                PopulateExistingModalPrefab(prefabPath, drawerName);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            var existing = FindDirectChild(modalRoot, drawerName);
            if (existing != null)
            {
                if (PrefabUtility.GetPrefabInstanceStatus(existing) != PrefabInstanceStatus.Connected)
                {
                    throw new InvalidOperationException("Main UI contains a same-name non-Prefab modal: " + GetTransformPath(existing.transform));
                }

                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate UI modal Prefab: " + prefabPath);
            }

            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetParent(modalRoot, false);
            ConfigureModalLayout(instance.transform as RectTransform, drawerName);
            instance.SetActive(false);
        }

        private static GameObject CreatePanelPrefab(string prefabPath, string panelName)
        {
            var temporary = new GameObject(panelName, typeof(RectTransform), typeof(Image));
            var image = temporary.GetComponent<Image>();
            image.color = panelName == "PlaybackControlsPanel"
                ? new Color(0.015f, 0.09f, 0.14f, 0.98f)
                : new Color(0.025f, 0.12f, 0.18f, 0.92f);
            PopulatePanelPrefabVisuals(temporary, panelName);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static GameObject CreateModalPrefab(string prefabPath, string drawerName)
        {
            var temporary = new GameObject(drawerName, typeof(RectTransform), typeof(Image));
            var image = temporary.GetComponent<Image>();
            image.color = drawerName == "FlightLegDrawer"
                ? new Color(0.015f, 0.075f, 0.1f, 0.98f)
                : new Color(0.015f, 0.075f, 0.1f, 0.98f);
            PopulateModalPrefabVisuals(temporary, drawerName);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temporary, prefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static void PopulateExistingPanelPrefab(string prefabPath, string panelName)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                PopulatePanelPrefabVisuals(contents, panelName);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void PopulateExistingModalPrefab(string prefabPath, string drawerName)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                PopulateModalPrefabVisuals(contents, drawerName);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void PopulatePanelPrefabVisuals(GameObject root, string panelName)
        {
            var index = 0;
            var title = CreateTextControl(root.transform, "TitleText", panelName, 18, ref index);
            title.alignment = TextAnchor.MiddleLeft;

            switch (panelName)
            {
                case "DashboardPanel":
                    PopulateReferenceGroup(root.transform, new DashboardPanelRefs(), "dashboard", false);
                    break;
                case "StatusPanel":
                    PopulateReferenceGroup(root.transform, new StatusPanelRefs(), "status", false);
                    break;
                case "DataInputPanel":
                    PopulateReferenceGroup(root.transform, new DataInputPanelRefs(), "dataInput", true);
                    break;
                case "PlaybackControlsPanel":
                    PopulateReferenceGroup(root.transform, new PlaybackControlsRefs(), "playback", false);
                    break;
                case "OceanCommandToolbar":
                    PopulateReferenceGroup(root.transform, new OceanToolbarRefs(), "oceanToolbar", false);
                    break;
            }
        }

        private static void PopulateModalPrefabVisuals(GameObject root, string drawerName)
        {
            var index = 0;
            CreateTextControl(root.transform, "TitleText", drawerName, 20, ref index);
            if (drawerName == "OceanCurrentDrawer")
            {
                PopulateReferenceGroup(root.transform, new OceanSectionRefs(), "oceanDrawer", false, ref index);
                PopulateReferenceGroup(root.transform, new DynamicsSectionRefs(), "dynamics", false, ref index);
            }
            else
            {
                PopulateReferenceGroup(root.transform, new FlightLegSectionRefs(), "flightLeg", false, ref index);
            }
        }

        private static void PopulateReferenceGroup(Transform parent, object group, string groupPath, bool dataInputPanel, ref int index)
        {
            foreach (var field in group.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (typeof(IUiReferenceGroup).IsAssignableFrom(field.FieldType))
                {
                    if (dataInputPanel && (field.Name == "dynamics" || field.Name == "flightLeg"))
                    {
                        continue;
                    }

                    var nested = field.GetValue(group);
                    if (nested != null)
                    {
                        PopulateReferenceGroup(parent, nested, groupPath + "." + field.Name, dataInputPanel, ref index);
                    }

                    continue;
                }

                if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)
                    || field.Name == "panel"
                    || (dataInputPanel && ShouldLiveInModal(groupPath, field.Name)))
                {
                    continue;
                }

                var objectName = GetUiObjectName(field.Name);
                if (FindDirectChild(parent, objectName) != null || FindDescendant(parent, objectName) != null)
                {
                    continue;
                }

                var created = CreateReferenceControl(parent, field.FieldType, objectName, ref index);
                if (field.GetCustomAttribute<OptionalUiReferenceAttribute>() != null)
                {
                    created.SetActive(false);
                }

                if (field.Name == "oceanLayerRowTemplate")
                {
                    var row = created.GetComponent<OceanCurrentLayerRowView>() ?? created.AddComponent<OceanCurrentLayerRowView>();
                    PopulateRowTemplate(row);
                }
            }
        }

        private static void PopulateReferenceGroup(Transform parent, object group, string groupPath, bool dataInputPanel)
        {
            var index = 0;
            PopulateReferenceGroup(parent, group, groupPath, dataInputPanel, ref index);
        }

        private static bool ShouldLiveInModal(string groupPath, string fieldName)
        {
            if (groupPath == "dataInput.dynamics" || groupPath == "dataInput.flightLeg")
            {
                return true;
            }

            if (groupPath != "dataInput.ocean")
            {
                return false;
            }

            return fieldName == "oceanCurrentDrawer"
                || fieldName.StartsWith("drawer", StringComparison.Ordinal)
                || fieldName == "qualitySummaryText"
                || fieldName == "prefetchHalfWidthInput"
                || fieldName == "forecastWindowInput"
                || fieldName == "fieldSummaryText"
                || fieldName == "onlineModeButton"
                || fieldName == "cacheOnlyModeButton"
                || fieldName == "localFileModeButton"
                || fieldName == "acquisitionModeText"
                || fieldName == "localFileInput"
                || fieldName == "actualSourceText";
        }

        private static GameObject CreateReferenceControl(Transform parent, Type fieldType, string objectName, ref int index)
        {
            var rect = new GameObject(objectName, typeof(RectTransform));
            rect.transform.SetParent(parent, false);
            ConfigureReferenceRect(rect.GetComponent<RectTransform>(), index++);

            if (fieldType == typeof(Text))
            {
                var text = rect.AddComponent<Text>();
                ConfigureText(text, objectName, 13);
            }
            else if (fieldType == typeof(Image))
            {
                rect.AddComponent<Image>().color = new Color(0.04f, 0.16f, 0.2f, 0.88f);
            }
            else if (fieldType == typeof(Button))
            {
                var image = rect.AddComponent<Image>();
                image.color = new Color(0.05f, 0.42f, 0.55f, 0.95f);
                var button = rect.AddComponent<Button>();
                button.targetGraphic = image;
                var label = CreateTextControl(rect.transform, objectName + "Label", objectName, 12, ref index);
                label.alignment = TextAnchor.MiddleCenter;
            }
            else if (fieldType == typeof(InputField))
            {
                var image = rect.AddComponent<Image>();
                image.color = new Color(0.02f, 0.1f, 0.14f, 0.96f);
                var input = rect.AddComponent<InputField>();
                var text = CreateTextControl(rect.transform, objectName + "Text", string.Empty, 13, ref index);
                input.textComponent = text;
            }
            else if (fieldType == typeof(Toggle))
            {
                rect.AddComponent<Toggle>();
                var label = CreateTextControl(rect.transform, objectName + "Label", objectName, 12, ref index);
                label.alignment = TextAnchor.MiddleLeft;
            }
            else if (fieldType == typeof(Slider))
            {
                rect.AddComponent<Slider>();
            }
            else if (fieldType == typeof(GameObject))
            {
                rect.AddComponent<Image>().color = new Color(0.04f, 0.16f, 0.2f, 0.88f);
            }

            return rect;
        }

        private static Text CreateTextControl(Transform parent, string objectName, string value, int fontSize, ref int index)
        {
            var objectToUse = FindDirectChild(parent, objectName);
            if (objectToUse == null)
            {
                objectToUse = new GameObject(objectName, typeof(RectTransform));
                objectToUse.transform.SetParent(parent, false);
                ConfigureReferenceRect(objectToUse.GetComponent<RectTransform>(), index++);
            }

            var text = objectToUse.GetComponent<Text>() ?? objectToUse.AddComponent<Text>();
            ConfigureText(text, value, fontSize);
            return text;
        }

        private static void ConfigureText(Text text, string value, int fontSize)
        {
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void ConfigureReferenceRect(RectTransform rect, int index)
        {
            var column = index % 3;
            var row = index / 3;
            rect.anchorMin = new Vector2(column / 3f, 1f);
            rect.anchorMax = new Vector2((column + 1) / 3f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -34f - row * 28f);
            rect.sizeDelta = new Vector2(-8f, 24f);
        }

        private static void PopulateRowTemplate(OceanCurrentLayerRowView row)
        {
            var index = 0;
            var title = CreateTextControl(row.transform, "TitleText", "Layer", 12, ref index);
            var depth = CreateTextControl(row.transform, "DepthRangeText", "0-0 m", 12, ref index);
            var velocity = CreateTextControl(row.transform, "VelocityText", "0 m/s", 12, ref index);
            var edit = CreateReferenceControl(row.transform, typeof(Button), "EditButton", ref index).GetComponent<Button>();
            var remove = CreateReferenceControl(row.transform, typeof(Button), "RemoveButton", ref index).GetComponent<Button>();
            var serialized = new SerializedObject(row);
            serialized.FindProperty("titleText").objectReferenceValue = title;
            serialized.FindProperty("depthRangeText").objectReferenceValue = depth;
            serialized.FindProperty("velocityText").objectReferenceValue = velocity;
            serialized.FindProperty("editButton").objectReferenceValue = edit;
            serialized.FindProperty("removeButton").objectReferenceValue = remove;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureModalLayout(RectTransform rect, string drawerName)
        {
            if (rect == null)
            {
                return;
            }

            if (drawerName == "FlightLegDrawer")
            {
                ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(660f, 350f));
            }
            else
            {
                ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 600f));
            }
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
                    ConfigureResponsivePanelRect(rect);
                    break;
                case "StatusPanel":
                    ConfigureResponsivePanelRect(rect);
                    break;
                case "DataInputPanel":
                    ConfigureResponsivePanelRect(rect);
                    break;
                case "PlaybackControlsPanel":
                    ConfigureResponsivePanelRect(rect);
                    break;
                case "OceanCommandToolbar":
                    ConfigureResponsivePanelRect(rect);
                    break;
            }
        }

        private static void MigrateLegacyPanelLayout(RectTransform rect, string panelName)
        {
            if (rect == null || !HasLegacyPanelLayout(rect, panelName))
            {
                return;
            }

            ConfigureResponsivePanelRect(rect);
        }

        private static bool HasLegacyPanelLayout(RectTransform rect, string panelName)
        {
            switch (panelName)
            {
                case "DashboardPanel":
                    return rect.sizeDelta.y < -100f;
                case "StatusPanel":
                    return rect.anchorMin.y > 0.9f && rect.sizeDelta.y > 60f;
                case "DataInputPanel":
                    return rect.anchorMax.y < 0.1f && rect.sizeDelta.y < 100f;
                case "PlaybackControlsPanel":
                    return rect.anchorMax.y < 0.1f && rect.sizeDelta.y >= 100f;
                case "OceanCommandToolbar":
                    return rect.anchorMin.x > 0.9f && rect.sizeDelta.x > 100f;
                default:
                    return false;
            }
        }

        private static void ConfigureResponsivePanelRect(RectTransform rect)
        {
            ConfigureRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -24f));
            var layout = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.preferredWidth = 0f;
            layout.flexibleWidth = 1f;
            layout.minHeight = 0f;
            layout.preferredHeight = 0f;
            layout.flexibleHeight = 1f;
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

        private static RectTransform EnsureLayoutContainer(Transform parent, string name)
        {
            var existing = FindDirectChild(parent, name);
            if (existing == null)
            {
                existing = new GameObject(name, typeof(RectTransform));
                existing.transform.SetParent(parent, false);
            }
            else if (existing.GetComponent<RectTransform>() == null)
            {
                throw new InvalidOperationException(
                    "Main UI contains a same-name non-layout object: " + GetTransformPath(existing.transform));
            }

            return existing.GetComponent<RectTransform>();
        }

        private static void MoveLegacyChildToContainer(Transform parent, string name, Transform destination)
        {
            var child = FindDirectChild(parent, name);
            if (child == null)
            {
                return;
            }

            if (destination == null)
            {
                throw new InvalidOperationException(
                    "Cannot migrate legacy UI object because its destination is missing: " + GetTransformPath(child.transform));
            }

            child.transform.SetParent(destination, false);
        }

        private static void EnsureDrawerEntryToggle(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var button = GetOrCreateButton(parent, name, label, anchoredPosition, size);
            var rect = button.GetComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, size);
            var labelText = button.transform.Find(name + "Label")?.GetComponent<Text>();
            if (labelText != null)
            {
                labelText.text = label;
                labelText.alignment = TextAnchor.MiddleCenter;
            }
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
