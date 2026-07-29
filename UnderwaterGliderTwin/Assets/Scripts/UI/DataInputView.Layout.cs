using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed partial class DataInputView
    {
        private RectTransform bottomDrawerContent;
        private RectTransform bottomDrawerViewport;
        private ScrollRect bottomDrawerScrollRect;
        private Button bottomDrawerToggleButton;
        private bool bottomDrawerExpanded;
        private Canvas oceanCurrentModalCanvas;
        private bool bottomDrawerExpandedBeforeModal;
        private const float ExpandedTaskDrawerHeight = 320f;

        private void ConfigureResponsiveBottomDrawer(RectTransform drawer)
        {
            if (drawer == null || bottomDrawerContent != null)
            {
                return;
            }

            drawer.anchorMin = new Vector2(0f, 1f);
            drawer.anchorMax = new Vector2(1f, 1f);
            drawer.pivot = new Vector2(0.5f, 1f);
            drawer.anchoredPosition = new Vector2(0f, -UiFactory.CommandCenterHeaderHeight);
            drawer.sizeDelta = new Vector2(0f, 48f);

            var header = UiFactory.Panel("MissionConfigurationDrawerHeader", drawer, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-16f, 44f), new Color(0.02f, 0.12f, 0.18f, 0.98f));
            UiFactory.Text("MissionConfigurationDrawerLabel", header, "任务参数", 16, TextAnchor.MiddleLeft, UiFactory.CommandText, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(180f, -8f));
            bottomDrawerToggleButton = UiFactory.Button("MissionConfigurationDrawerToggleButton", header, "展开参数", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(108f, 30f));
            bottomDrawerToggleButton.onClick.AddListener(ToggleBottomDrawer);

            bottomDrawerViewport = UiFactory.Panel("MissionConfigurationViewport", drawer, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.08f));
            bottomDrawerViewport.offsetMin = new Vector2(10f, 10f);
            bottomDrawerViewport.offsetMax = new Vector2(-10f, -54f);
            bottomDrawerViewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            bottomDrawerViewport.GetComponent<Image>().raycastTarget = false;
            bottomDrawerContent = UiFactory.Panel("MissionConfigurationContent", bottomDrawerViewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f), Color.clear);
            var contentLayout = bottomDrawerContent.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 6, 8);
            contentLayout.spacing = 6f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var fitter = bottomDrawerContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bottomDrawerScrollRect = drawer.gameObject.AddComponent<ScrollRect>();
            bottomDrawerScrollRect.viewport = bottomDrawerViewport;
            bottomDrawerScrollRect.content = bottomDrawerContent;
            bottomDrawerScrollRect.horizontal = false;
            bottomDrawerScrollRect.vertical = true;
            bottomDrawerScrollRect.movementType = ScrollRect.MovementType.Clamped;

            var missionSection = CreateSectionCard("MissionSectionCard", "任务与预测", bottomDrawerContent);
            var simulationSection = CreateSectionCard("SimulationSectionCard", "仿真参数", bottomDrawerContent);
            var oceanSection = CreateSectionCard("OceanSectionCard", "海流与航段", bottomDrawerContent);

            CreateCompositeField(missionSection, "CsvSourceLabel", "CsvPathInput", "LoadCsvButton", "CsvPathField", true);
            CreateCompositeField(missionSection, "PredictionModelLabel", FindExistingModelButtonName(), null, "PredictionModelField", false);
            CreateCompositeField(missionSection, "PredictionHorizonLabel", "PredictionHorizonInput", "ApplyPredictionConfigButton", "PredictionHorizonField", false);
            CreateCompositeField(missionSection, null, "PredictionToggleButton", "PredictionRuntimeLabel", "PredictionRuntimeField", false);

            MoveToSection(simulationSection, "SimulationCyclesInputField");
            MoveToSection(simulationSection, "SimulationDurationInputField");
            MoveToSection(simulationSection, "SimulationDepthInputField");
            MoveToSection(simulationSection, "SimulationWaterColumnInputField");
            MoveToSection(simulationSection, "ReferenceCycleDurationLabel");
            MoveToSection(simulationSection, "ReferenceCycleDurationReadout");
            MoveToSection(simulationSection, "ApplyReferenceCycleButton");
            MoveToSection(simulationSection, "SimulationHeadingInputField");
            MoveToSection(simulationSection, "SimulationHeadingDeltaInputField");
            MoveToSection(simulationSection, "SimulationPitchInputField");
            MoveToSection(simulationSection, "SimulationRollInputField");
            MoveToSection(simulationSection, "SimulationApplyButton");
            MoveToSection(simulationSection, "FlightLegSettingsButton");

            MoveToSection(oceanSection, "OceanCurrentMinDepthInputField");
            MoveToSection(oceanSection, "OceanCurrentMaxDepthInputField");
            MoveToSection(oceanSection, "OceanCurrentEastwardInputField");
            MoveToSection(oceanSection, "OceanCurrentNorthwardInputField");
            MoveToSection(oceanSection, "MissionLongitudeInputField");
            MoveToSection(oceanSection, "MissionLatitudeInputField");
            MoveToSection(oceanSection, "OceanCurrentPreviousLayerButton");
            MoveToSection(oceanSection, "OceanCurrentNextLayerButton");
            MoveToSection(oceanSection, "OceanCurrentAddLayerButton");
            MoveToSection(oceanSection, "OceanCurrentSaveLayerButton");
            MoveToSection(oceanSection, "OceanCurrentDeleteLayerButton");
            MoveToSection(oceanSection, "OceanCurrentLookupButton");
            MoveToSection(oceanSection, "OceanCurrentLayerSummary");
            MoveToSection(oceanSection, "OceanCurrentDrawerButton");

            HideLegacyTextChild("MissionConfigurationTitle");
            HideLegacyTextChild("SimulationLabel");
            HideLegacyTextChild("OceanCurrentLabel");
            MoveToSectionFooter(missionSection, "MissionConfigurationStatus");
            HideLegacyConfigurationGroups(drawer);

            SetBottomDrawerExpanded(false);
        }

        private RectTransform CreateSectionCard(string name, string title, Transform parent)
        {
            var section = UiFactory.Panel(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 0f), new Color(0.02f, 0.12f, 0.18f, 0.88f));
            var sectionLayout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            sectionLayout.padding = new RectOffset(10, 10, 6, 7);
            sectionLayout.spacing = 4f;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;
            var sectionFitter = section.gameObject.AddComponent<ContentSizeFitter>();
            sectionFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sectionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var titleText = UiFactory.Text(name + "Title", section, title, 15, TextAnchor.MiddleLeft, UiFactory.CommandText, Vector2.zero, new Vector2(0f, 24f));
            titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            var fields = UiFactory.Panel(name.Replace("Card", "Fields"), section, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            var grid = fields.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.spacing = new Vector2(8f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;
            var fieldsFitter = fields.gameObject.AddComponent<ContentSizeFitter>();
            fieldsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fieldsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var responsive = fields.gameObject.AddComponent<ResponsiveTaskParameterLayout>();
            responsive.Configure(grid, fieldsFitter);
            return section;
        }

        private string FindExistingModelButtonName()
        {
            foreach (var entry in modelButtons)
            {
                if (entry.Value != null)
                {
                    return entry.Value.name;
                }
            }

            return null;
        }

        private void CreateCompositeField(Transform section, string labelName, string controlName, string secondaryName, string wrapperName, bool fullWidth)
        {
            if (string.IsNullOrWhiteSpace(controlName) && string.IsNullOrWhiteSpace(labelName))
            {
                return;
            }

            var fields = section.Find(section.name.Replace("Card", "Fields"));
            if (fields == null)
            {
                return;
            }

            var card = UiFactory.Panel(wrapperName, fields, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.04f, 0.18f, 0.24f, 0.8f));
            var cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(8, 8, 5, 5);
            cardLayout.spacing = 4f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = fullWidth ? 62f : 56f;
            element.minWidth = fullWidth ? 560f : 240f;
            if (labelName != null)
            {
                MoveLeaf(labelName, card, 18f);
            }
            if (controlName != null)
            {
                MoveLeaf(controlName, card, 28f);
            }
            if (secondaryName != null)
            {
                MoveLeaf(secondaryName, card, 28f);
            }
        }

        private void MoveToSection(Transform section, string childName)
        {
            var child = FindDirectChild(childName);
            if (child == null)
            {
                return;
            }

            var fields = section.Find(section.name.Replace("Card", "Fields"));
            if (fields == null)
            {
                return;
            }

            child.SetParent(fields, false);
            NormalizeLayoutChild(child);
            NormalizeFieldCard(child);
        }

        private void MoveToSectionFooter(Transform section, string childName)
        {
            var child = FindDirectChild(childName);
            if (child == null)
            {
                return;
            }

            child.SetParent(section, false);
            child.SetAsLastSibling();
            NormalizeLayoutChild(child, 24f);
            var text = child.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleLeft;
            }
        }

        private void HideLegacyTextChild(string childName)
        {
            var child = FindDirectChild(childName);
            if (child == null)
            {
                return;
            }

            var text = child.GetComponent<Text>();
            if (text != null)
            {
                var color = text.color;
                color.a = 0f;
                text.color = color;
            }

            child.SetParent(bottomDrawerContent, false);
            var element = child.gameObject.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            child.sizeDelta = Vector2.zero;
        }

        private void MoveLeaf(string childName, Transform parent, float preferredHeight)
        {
            var child = FindDirectChild(childName);
            if (child == null)
            {
                return;
            }

            child.SetParent(parent, false);
            NormalizeLayoutChild(child, preferredHeight);
        }

        private RectTransform FindDirectChild(string childName)
        {
            return string.IsNullOrWhiteSpace(childName) || configurationPanel == null
                ? null
                : configurationPanel.Find(childName) as RectTransform;
        }

        private static void NormalizeLayoutChild(RectTransform child, float preferredHeight = 54f)
        {
            child.anchorMin = new Vector2(0f, 0.5f);
            child.anchorMax = new Vector2(1f, 0.5f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.anchoredPosition = Vector2.zero;
            child.sizeDelta = new Vector2(0f, preferredHeight);
            var element = child.gameObject.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            element.minHeight = preferredHeight;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = 1f;
        }

        private static void NormalizeFieldCard(RectTransform field)
        {
            if (field == null || !field.name.EndsWith("Field"))
            {
                return;
            }

            var image = field.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.035f, 0.16f, 0.22f, 0.9f);
            }

            var layout = field.gameObject.GetComponent<VerticalLayoutGroup>() ?? field.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 5);
            layout.spacing = 3f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            foreach (Transform child in field)
            {
                var childRect = child as RectTransform;
                if (childRect == null)
                {
                    continue;
                }

                var input = child.GetComponent<InputField>();
                var height = input != null ? 26f : 16f;
                NormalizeLayoutChild(childRect, height);
            }
        }

        private static void HideLegacyConfigurationGroups(RectTransform drawer)
        {
            var names = new[] { "DataSourceConfigurationGroup", "PredictionConfigurationGroup", "SimulationConfigurationGroup", "OceanConfigurationGroup" };
            foreach (var name in names)
            {
                var group = drawer.Find(name) as RectTransform;
                if (group == null)
                {
                    continue;
                }

                group.sizeDelta = Vector2.zero;
                var image = group.GetComponent<Image>();
                if (image != null)
                {
                    image.enabled = false;
                }
                var element = group.gameObject.GetComponent<LayoutElement>() ?? group.gameObject.AddComponent<LayoutElement>();
                element.ignoreLayout = true;
            }
        }

        private Canvas EnsureOceanCurrentModalCanvas(Transform mainCanvas)
        {
            if (oceanCurrentModalCanvas != null)
            {
                return oceanCurrentModalCanvas;
            }

            var main = mainCanvas != null ? mainCanvas.GetComponent<Canvas>() : null;
            var modalObject = new GameObject("OceanCurrentModalCanvas");
            modalObject.transform.SetParent(mainCanvas, false);
            var modalRect = modalObject.AddComponent<RectTransform>();
            var mainRect = mainCanvas as RectTransform;
            var modalSize = mainRect != null && mainRect.rect.width > 0f && mainRect.rect.height > 0f
                ? mainRect.rect.size
                : new Vector2(1920f, 1080f);
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.anchoredPosition = Vector2.zero;
            modalRect.sizeDelta = modalSize;
            oceanCurrentModalCanvas = modalObject.AddComponent<Canvas>();
            oceanCurrentModalCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            oceanCurrentModalCanvas.overrideSorting = true;
            oceanCurrentModalCanvas.sortingOrder = (main != null ? main.sortingOrder : 10) + 20;
            modalObject.AddComponent<GraphicRaycaster>();

            var blocker = UiFactory.Panel("OceanCurrentModalRaycastBlocker", modalObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.18f));
            blocker.GetComponent<Image>().raycastTarget = true;
            blocker.SetAsFirstSibling();
            modalObject.SetActive(false);
            return oceanCurrentModalCanvas;
        }

        private void ToggleBottomDrawer()
        {
            SetBottomDrawerExpanded(!bottomDrawerExpanded);
        }

        private void SetBottomDrawerExpanded(bool expanded)
        {
            bottomDrawerExpanded = expanded;
            var drawer = bottomDrawerContent != null ? bottomDrawerContent.parent?.parent as RectTransform : null;
            if (drawer == null)
            {
                return;
            }

            // CanvasScaler makes this 35% at both 1280x720 and 1920x1080.
            drawer.sizeDelta = new Vector2(0f, expanded ? ExpandedTaskDrawerHeight : 48f);
            if (bottomDrawerToggleButton != null)
            {
                UiFactory.SetButtonText(bottomDrawerToggleButton, expanded ? "收起参数" : "展开参数");
            }
        }

        private void OnActiveRuntimeSessionChanged(SimulationRuntimeSession session)
        {
            AttachRuntimeSession(session);
        }

        private void AttachRuntimeSession(SimulationRuntimeSession session)
        {
            if (ReferenceEquals(subscribedRuntimeSession, session))
            {
                RefreshRuntimeStatus();
                return;
            }

            if (subscribedRuntimeSession != null)
            {
                subscribedRuntimeSession.StatusChanged -= RefreshRuntimeStatus;
            }

            subscribedRuntimeSession = session;
            if (subscribedRuntimeSession != null)
            {
                subscribedRuntimeSession.StatusChanged += RefreshRuntimeStatus;
                RefreshRuntimeStatus();
            }
        }

        private static void ConfigureInlineDrawer(RectTransform drawer)
        {
            if (drawer == null)
            {
                return;
            }

            // Modal editors are constrained to the same bottom safe area as the main drawer.
            // They never cover more than 35% of the screen, preserving the 3D interaction area.
            drawer.anchorMin = new Vector2(0f, 0f);
            drawer.anchorMax = new Vector2(1f, 0f);
            drawer.pivot = new Vector2(0.5f, 0f);
            drawer.anchoredPosition = Vector2.zero;
            drawer.sizeDelta = new Vector2(-24f, 48f);
            var heightLimiter = drawer.gameObject.GetComponent<DrawerHeightLimiter>() ?? drawer.gameObject.AddComponent<DrawerHeightLimiter>();
            heightLimiter.Apply();

            if (drawer.Find("EditorViewport") != null)
            {
                return;
            }

            // Preserve the stable child names and their existing event handlers, while placing
            // the legacy fixed-coordinate editor surface inside a clipped vertical ScrollRect.
            var existingChildren = new System.Collections.Generic.List<Transform>();
            for (var index = 0; index < drawer.childCount; index++)
            {
                existingChildren.Add(drawer.GetChild(index));
            }
            var viewportObject = new GameObject("EditorViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(drawer, false);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;
            var contentObject = new GameObject("EditorContent", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            var content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var availableWidth = Mathf.Max(1f, drawer.rect.width - 24f);
            var contentWidth = drawer.name == "FlightLegDrawerPanel"
                ? drawer.rect.width + 16f
                : Mathf.Min(760f, availableWidth);
                content.sizeDelta = new Vector2(contentWidth, drawer.name == "FlightLegDrawerPanel" ? 350f : 620f);
            foreach (var child in existingChildren)
            {
                child.SetParent(content, false);
            }
            var scroll = drawer.gameObject.GetComponent<ScrollRect>() ?? drawer.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        private sealed class DrawerHeightLimiter : MonoBehaviour
        {
            private RectTransform rect;

            private void Awake()
            {
                rect = transform as RectTransform;
            }

            private void OnEnable()
            {
                Apply();
            }

            private void OnRectTransformDimensionsChange()
            {
                Apply();
            }

            public void Apply()
            {
                if (rect == null)
                {
                    rect = transform as RectTransform;
                }
                var canvas = GetComponentInParent<Canvas>();
                var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
                if (rect == null || canvasRect == null || canvasRect.rect.height <= 0f)
                {
                    return;
                }
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, canvasRect.rect.height * 0.35f);
            }
        }

        private void RefreshRuntimeStatus()
        {
            if (subscribedRuntimeSession == null)
            {
                return;
            }

            if (subscribedRuntimeSession.IsRebuildPending)
            {
                SetStatus("参数仿真正在重建，当前轨迹保持不变…", new Color(0.62f, 0.85f, 0.92f));
                return;
            }

            if (!string.IsNullOrWhiteSpace(subscribedRuntimeSession.LastError))
            {
                SetStatus("参数仿真未更新：" + subscribedRuntimeSession.LastError, new Color(1f, 0.58f, 0.58f));
                return;
            }

            SetStatus("参数仿真已更新", new Color(0.74f, 0.95f, 1f));
        }
    }

    public sealed class ResponsiveTaskParameterLayout : MonoBehaviour
    {
        private const float MinimumCardWidth = 320f;
        private const float HorizontalGap = 8f;
        private const float VerticalGap = 4f;
        private GridLayoutGroup grid;
        private ContentSizeFitter fitter;

        public int ColumnCount => grid != null ? grid.constraintCount : 0;

        public void Configure(GridLayoutGroup targetGrid, ContentSizeFitter targetFitter)
        {
            grid = targetGrid;
            fitter = targetFitter;
            ApplyForWidth((transform as RectTransform)?.rect.width ?? 1920f);
        }

        public void RefreshForWidth(float width)
        {
            ApplyForWidth(width);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (grid != null)
            {
                ApplyForWidth((transform as RectTransform)?.rect.width ?? 1920f);
            }
        }

        private void ApplyForWidth(float width)
        {
            if (grid == null)
            {
                return;
            }

            var available = Mathf.Max(0f, width - grid.padding.horizontal);
            var columns = available > MinimumCardWidth * 4f + HorizontalGap * 3f
                ? 4
                : available > MinimumCardWidth * 3f + HorizontalGap * 2f
                    ? 3
                    : available >= MinimumCardWidth * 2f + HorizontalGap
                        ? 2
                        : 1;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.spacing = new Vector2(HorizontalGap, VerticalGap);
            var cellWidth = Mathf.Max(MinimumCardWidth, (available - HorizontalGap * (columns - 1)) / columns);
            grid.cellSize = new Vector2(cellWidth, 54f);
            if (fitter != null)
            {
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
    }
}
