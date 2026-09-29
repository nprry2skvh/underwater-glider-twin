using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public sealed class ReferenceHudLayoutController : MonoBehaviour
    {
        public const string ConfigurationDrawerId = "configuration";
        public const string TelemetryDrawerId = "telemetry";
        public const string StatusDrawerId = "status";
        public const string ControlsDrawerId = "controls";

        private const string LeftDrawerGroup = "left";
        private const string RightDrawerGroup = "right";
        private const string BottomDrawerGroup = "bottom";
        private const float TopMargin = 60f;
        private static readonly Color QgcToolbar = new Color(0.105f, 0.125f, 0.14f, 0.98f);
        private static readonly Color QgcPanel = new Color(0.12f, 0.14f, 0.155f, 0.95f);
        private static readonly Color QgcPanelHeader = new Color(0.155f, 0.18f, 0.195f, 0.98f);
        private static readonly Color QgcControl = new Color(0.19f, 0.22f, 0.24f, 0.98f);
        private static readonly Color QgcBorder = new Color(0.36f, 0.4f, 0.43f, 0.9f);
        private static readonly Color QgcAccent = new Color(0.14f, 0.58f, 0.78f, 1f);
        private static readonly Color QgcAccentBright = new Color(0.28f, 0.72f, 0.92f, 1f);
        private static readonly Color QgcTextDim = new Color(0.72f, 0.76f, 0.79f, 1f);

        private sealed class DrawerBinding
        {
            public string Id;
            public string Group;
            public Button Tab;
            public readonly List<GameObject> Panels = new List<GameObject>();
            public bool IsOpen;
        }

        private readonly List<DrawerBinding> drawers = new List<DrawerBinding>();
        private Canvas runtimeCanvas;
        private RectTransform tabLayer;
        private RectTransform header;
        private RectTransform configurationPanel;
        private RectTransform telemetryPanel;
        private RectTransform navigationCard;
        private RectTransform statusPanel;
        private RectTransform playbackPanel;
        private RectTransform oceanToolbar;
        private RectTransform viewportFrame;
        private Vector2 lastCanvasSize;

        public static ReferenceHudLayoutController Install(GameObject runtimeRoot)
        {
            if (runtimeRoot == null)
            {
                return null;
            }

            var canvas = runtimeRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = runtimeRoot.GetComponentInChildren<Canvas>(true);
            }
            if (canvas == null)
            {
                return null;
            }

            var controller = canvas.GetComponent<ReferenceHudLayoutController>();
            if (controller == null)
            {
                controller = canvas.gameObject.AddComponent<ReferenceHudLayoutController>();
            }
            controller.Initialize(canvas);
            return controller;
        }

        public bool IsDrawerOpen(string drawerId)
        {
            var drawer = FindDrawer(drawerId);
            return drawer != null && drawer.IsOpen;
        }

        public void ToggleDrawer(string drawerId)
        {
            var drawer = FindDrawer(drawerId);
            if (drawer != null)
            {
                SetDrawerOpen(drawerId, !drawer.IsOpen);
            }
        }

        public void SetDrawerOpen(string drawerId, bool open)
        {
            var drawer = FindDrawer(drawerId);
            if (drawer == null)
            {
                return;
            }

            if (open)
            {
                foreach (var other in drawers)
                {
                    if (!ReferenceEquals(other, drawer) && other.Group == drawer.Group)
                    {
                        ApplyDrawerState(other, false);
                    }
                }
            }

            ApplyDrawerState(drawer, open);
            BringTabsToFront();
        }

        private void Initialize(Canvas canvas)
        {
            runtimeCanvas = canvas;
            header = FindRect("CommandCenterHeader");
            configurationPanel = FindRect("MissionConfigurationPanel");
            telemetryPanel = FindRect("TelemetryPanel");
            navigationCard = FindRect("NavigationReferenceCard");
            statusPanel = FindRect("MissionStatusPanel");
            playbackPanel = FindRect("PlaybackControlsPanel");
            oceanToolbar = FindRect("OceanCommandToolbar");
            viewportFrame = FindRect("OceanViewportFrame");

            RemoveExistingTabLayer();
            ConfigureLayout();
            BuildDrawers();
            ApplyQGroundControlTheme();
            BringTabsToFront();
        }

        private void LateUpdate()
        {
            if (runtimeCanvas == null)
            {
                return;
            }

            var canvasRect = runtimeCanvas.transform as RectTransform;
            if (canvasRect == null || canvasRect.rect.size == lastCanvasSize)
            {
                return;
            }

            ConfigureLayout();
            BringTabsToFront();
        }

        private void ConfigureLayout()
        {
            var canvasSize = GetCanvasSize();
            lastCanvasSize = canvasSize;
            ConfigureHeader();
            ConfigureConfigurationDrawer(canvasSize);
            ConfigureTelemetryDrawer(canvasSize);
            ConfigureStatusDrawer(canvasSize);
            ConfigurePlaybackBar(canvasSize);
            ConfigureOceanToolbar();
            if (viewportFrame != null)
            {
                viewportFrame.gameObject.SetActive(false);
            }
        }

        private void ConfigureHeader()
        {
            if (header == null)
            {
                return;
            }

            SetFixedRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 52f));
            var headerImage = header.GetComponent<Image>();
            if (headerImage != null)
            {
                headerImage.color = QgcToolbar;
            }

            var headerOutline = header.GetComponent<Outline>() ?? header.gameObject.AddComponent<Outline>();
            headerOutline.effectColor = new Color(0.42f, 0.46f, 0.49f, 0.86f);
            headerOutline.effectDistance = new Vector2(0f, -1f);

            ConfigureHeaderText("CommandCenterProductName", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(218f, 32f), 16, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white);
            ConfigureHeaderText("CommandCenterSystemHealth", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-190f, 0f), new Vector2(96f, 24f), 12, TextAnchor.MiddleRight, FontStyle.Bold, new Color(0.48f, 0.92f, 0.64f, 1f));
            ConfigureHeaderText("CommandCenterRuntime", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(156f, 24f), 12, TextAnchor.MiddleRight, FontStyle.Normal, QgcTextDim);
            EnsureQgcToolbarDecorations();

            var missionButton = FindRect("CommandCenterMissionConfigButton");
            if (missionButton != null)
            {
                SetFixedRect(missionButton, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(246f, 0f), new Vector2(122f, 34f));
                var button = missionButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => ToggleDrawer(ConfigurationDrawerId));
                    button.image.color = Color.white;
                    var colors = button.colors;
                    colors.normalColor = QgcAccent;
                    colors.highlightedColor = QgcAccentBright;
                    colors.pressedColor = new Color(0.08f, 0.42f, 0.62f, 1f);
                    button.colors = colors;

                    var label = button.GetComponentInChildren<Text>(true);
                    if (label != null)
                    {
                        label.color = Color.white;
                        label.fontSize = 12;
                        label.fontStyle = FontStyle.Bold;
                    }

                    var outline = button.GetComponent<Outline>();
                    if (outline != null)
                    {
                        outline.effectColor = QgcAccentBright;
                    }
                }
            }
        }

        private void EnsureQgcToolbarDecorations()
        {
            var layout = header.Find("QgcToolbarDecorations") as RectTransform;
            if (layout != null)
            {
                return;
            }

            var layoutObject = new GameObject("QgcToolbarDecorations", typeof(RectTransform));
            layoutObject.transform.SetParent(header, false);
            layout = layoutObject.GetComponent<RectTransform>();
            layout.anchorMin = Vector2.zero;
            layout.anchorMax = Vector2.one;
            layout.offsetMin = Vector2.zero;
            layout.offsetMax = Vector2.zero;

            var mode = UiFactory.Text("QgcFlightViewLabel", layout, "实时任务", 13, TextAnchor.MiddleLeft, QgcTextDim,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(388f, 0f), new Vector2(110f, 28f));
            mode.fontStyle = FontStyle.Bold;
            mode.raycastTarget = false;

            var activeLine = UiFactory.Panel("QgcFlightViewActiveLine", layout, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(388f, 0f), new Vector2(82f, 3f), QgcAccentBright);
            activeLine.GetComponent<Image>().raycastTarget = false;

            var divider = UiFactory.Panel("QgcToolbarDivider", layout, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 1f), new Color(0.46f, 0.5f, 0.53f, 0.7f));
            divider.GetComponent<Image>().raycastTarget = false;
        }

        private void ConfigureConfigurationDrawer(Vector2 canvasSize)
        {
            if (configurationPanel == null)
            {
                return;
            }

            var width = canvasSize.x < 1500f ? 620f : 700f;
            var height = Mathf.Clamp(canvasSize.y - 96f, 560f, 850f);
            SetFixedRect(configurationPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(78f, -TopMargin), new Vector2(width, height));
            SetPanelOpacity(configurationPanel, 0.96f);

            var drawerHeader = FindRect("MissionConfigurationDrawerHeader");
            if (drawerHeader != null)
            {
                drawerHeader.offsetMin = new Vector2(8f, -44f);
                drawerHeader.offsetMax = new Vector2(-8f, 0f);
            }

            var viewport = FindRect("MissionConfigurationViewport");
            if (viewport != null)
            {
                viewport.offsetMin = new Vector2(10f, 42f);
                viewport.offsetMax = new Vector2(-10f, -52f);
            }

            var status = FindRect("MissionConfigurationStatus");
            if (status != null)
            {
                SetFixedRect(status, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-28f, 22f));
            }

            var closeButton = FindRect("MissionConfigurationDrawerToggleButton")?.GetComponent<Button>();
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => SetDrawerOpen(ConfigurationDrawerId, false));
                UiFactory.SetButtonText(closeButton, "收起面板");
            }

            foreach (var responsiveLayout in configurationPanel.GetComponentsInChildren<ResponsiveTaskParameterLayout>(true))
            {
                responsiveLayout.RefreshForWidth(width - 44f);
            }
        }

        private void ConfigureTelemetryDrawer(Vector2 canvasSize)
        {
            if (telemetryPanel != null)
            {
                SetFixedRect(telemetryPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -TopMargin), telemetryPanel.sizeDelta);
                SetPanelOpacity(telemetryPanel, 0.94f);
            }

            if (navigationCard != null)
            {
                var telemetryHeight = telemetryPanel != null ? telemetryPanel.sizeDelta.y : 384f;
                SetFixedRect(navigationCard, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -TopMargin - telemetryHeight - 8f), navigationCard.sizeDelta);
                SetPanelOpacity(navigationCard, 0.92f);
            }
        }

        private void ConfigureStatusDrawer(Vector2 canvasSize)
        {
            if (statusPanel == null)
            {
                return;
            }

            var availableHeight = Mathf.Max(360f, canvasSize.y - TopMargin - 36f);
            var size = statusPanel.sizeDelta;
            size.y = Mathf.Min(size.y, availableHeight);
            SetFixedRect(statusPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -TopMargin), size);
            SetPanelOpacity(statusPanel, 0.94f);
        }

        private void ConfigurePlaybackBar(Vector2 canvasSize)
        {
            if (playbackPanel == null)
            {
                return;
            }

            var width = Mathf.Clamp(canvasSize.x - 240f, 1320f, 1480f);
            SetFixedRect(playbackPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(34f, 12f), new Vector2(width, 124f));
            SetPanelOpacity(playbackPanel, 0.94f);
        }

        private void ConfigureOceanToolbar()
        {
            if (oceanToolbar == null)
            {
                return;
            }

            SetFixedRect(oceanToolbar, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(78f, 146f), new Vector2(880f, 58f));
            SetPanelOpacity(oceanToolbar, 0.94f);
        }

        private void BuildDrawers()
        {
            drawers.Clear();
            tabLayer = UiFactory.Panel("HudDrawerTabLayer", runtimeCanvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear);
            tabLayer.GetComponent<Image>().raycastTarget = false;
            CreateQgcToolStrip(tabLayer);

            var configuration = CreateDrawer(ConfigurationDrawerId, LeftDrawerGroup, "任务", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -68f), new Vector2(54f, 48f), configurationPanel);
            var telemetry = CreateDrawer(TelemetryDrawerId, RightDrawerGroup, "遥测", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -120f), new Vector2(54f, 48f), telemetryPanel, navigationCard);
            var status = CreateDrawer(StatusDrawerId, RightDrawerGroup, "状态", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -172f), new Vector2(54f, 48f), statusPanel);
            var controls = CreateDrawer(ControlsDrawerId, BottomDrawerGroup, "回放", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -224f), new Vector2(54f, 48f), playbackPanel);

            ApplyDrawerState(configuration, false);
            ApplyDrawerState(telemetry, false);
            ApplyDrawerState(status, false);
            ApplyDrawerState(controls, true);
        }

        private DrawerBinding CreateDrawer(string id, string group, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, params RectTransform[] panels)
        {
            var binding = new DrawerBinding { Id = id, Group = group };
            foreach (var panel in panels)
            {
                if (panel != null)
                {
                    binding.Panels.Add(panel.gameObject);
                }
            }

            if (binding.Panels.Count == 0)
            {
                return null;
            }

            var tab = UiFactory.Button("Hud" + char.ToUpperInvariant(id[0]) + id.Substring(1) + "Tab", tabLayer, label, anchorMin, anchorMax, pivot, position, size);
            tab.onClick.AddListener(() => ToggleDrawer(id));
            var tabLabel = tab.GetComponentInChildren<Text>(true);
            if (tabLabel != null)
            {
                tabLabel.fontSize = 11;
                tabLabel.fontStyle = FontStyle.Bold;
                tabLabel.lineSpacing = 1f;
            }
            EnsureActiveIndicator(tab.transform as RectTransform);
            binding.Tab = tab;
            drawers.Add(binding);
            return binding;
        }

        private static void CreateQgcToolStrip(Transform parent)
        {
            var strip = UiFactory.Panel("QgcToolStrip", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -60f), new Vector2(62f, 220f), new Color(0.09f, 0.105f, 0.115f, 0.96f));
            strip.GetComponent<Image>().raycastTarget = false;
            var outline = strip.gameObject.AddComponent<Outline>();
            outline.effectColor = QgcBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            strip.SetAsFirstSibling();
        }

        private static void EnsureActiveIndicator(RectTransform tab)
        {
            if (tab == null)
            {
                return;
            }

            var indicator = UiFactory.Panel("QgcActiveIndicator", tab, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f), QgcAccentBright);
            indicator.GetComponent<Image>().raycastTarget = false;
        }

        private void ApplyDrawerState(DrawerBinding drawer, bool open)
        {
            if (drawer == null)
            {
                return;
            }

            drawer.IsOpen = open;
            foreach (var panel in drawer.Panels)
            {
                if (panel != null)
                {
                    panel.SetActive(open);
                    if (open)
                    {
                        panel.transform.SetAsLastSibling();
                    }
                }
            }

            if (drawer.Tab != null)
            {
                var tabColor = open ? new Color(0.2f, 0.24f, 0.26f, 1f) : new Color(0.12f, 0.14f, 0.15f, 0.98f);
                drawer.Tab.image.color = Color.white;
                var colors = drawer.Tab.colors;
                colors.normalColor = tabColor;
                colors.highlightedColor = new Color(0.25f, 0.3f, 0.33f, 1f);
                colors.pressedColor = new Color(0.07f, 0.09f, 0.1f, 1f);
                colors.selectedColor = colors.highlightedColor;
                drawer.Tab.colors = colors;
                var outline = drawer.Tab.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = open ? QgcAccentBright : QgcBorder;
                }

                var indicator = drawer.Tab.transform.Find("QgcActiveIndicator");
                if (indicator != null)
                {
                    indicator.gameObject.SetActive(open);
                }
            }
        }

        private void ApplyQGroundControlTheme()
        {
            StyleQgcPanel(configurationPanel, 0.97f);
            StyleQgcPanel(telemetryPanel, 0.96f);
            StyleQgcPanel(navigationCard, 0.94f);
            StyleQgcPanel(statusPanel, 0.96f);
            StyleQgcPanel(playbackPanel, 0.94f);
            StyleQgcPanel(oceanToolbar, 0.94f);
            StyleQgcPanel(FindRect("OceanCurrentDrawerPanel"), 0.98f);
            StyleQgcPanel(FindRect("FlightLegDrawerPanel"), 0.98f);
            StyleQgcPanel(FindRect("MissionConfigurationDrawerHeader"), 0.98f);
            StyleQgcNestedPanels();

            foreach (var button in runtimeCanvas.GetComponentsInChildren<Button>(true))
            {
                StyleQgcButton(button);
            }

            foreach (var input in runtimeCanvas.GetComponentsInChildren<InputField>(true))
            {
                StyleQgcInput(input);
            }

            foreach (var toggle in runtimeCanvas.GetComponentsInChildren<Toggle>(true))
            {
                StyleQgcToggle(toggle);
            }

            foreach (var slider in runtimeCanvas.GetComponentsInChildren<Slider>(true))
            {
                StyleQgcSlider(slider);
            }

            foreach (var text in runtimeCanvas.GetComponentsInChildren<Text>(true))
            {
                if (text.name.IndexOf("Title", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || text.name.IndexOf("HeaderLabel", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || text.name.IndexOf("SectionLabel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    text.color = Color.white;
                    text.fontStyle = FontStyle.Bold;
                }
                else if (text.name.IndexOf("GroupLabel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    text.color = QgcTextDim;
                }
            }

            foreach (var drawer in drawers)
            {
                ApplyDrawerState(drawer, drawer.IsOpen);
            }
        }

        private void StyleQgcNestedPanels()
        {
            foreach (var rect in runtimeCanvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == null || rect == configurationPanel || rect == telemetryPanel || rect == navigationCard
                    || rect == statusPanel || rect == playbackPanel || rect == oceanToolbar)
                {
                    continue;
                }

                var isStructuredPanel = rect.name.EndsWith("SectionCard", System.StringComparison.Ordinal)
                    || rect.name.EndsWith("DrawerPanel", System.StringComparison.Ordinal)
                    || rect.name.EndsWith("DrawerHeader", System.StringComparison.Ordinal);
                if (!isStructuredPanel)
                {
                    continue;
                }

                var image = rect.GetComponent<Image>();
                if (image != null)
                {
                    image.color = rect.name.EndsWith("DrawerHeader", System.StringComparison.Ordinal)
                        ? QgcPanelHeader
                        : new Color(0.145f, 0.165f, 0.18f, 0.98f);
                }

                var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.3f, 0.34f, 0.37f, 0.85f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }

        private static void StyleQgcPanel(RectTransform panel, float alpha)
        {
            if (panel == null)
            {
                return;
            }

            var image = panel.GetComponent<Image>();
            if (image != null)
            {
                var color = QgcPanel;
                color.a = alpha;
                image.color = color;
            }

            var outline = panel.GetComponent<Outline>() ?? panel.gameObject.AddComponent<Outline>();
            outline.effectColor = QgcBorder;
            outline.effectDistance = new Vector2(1f, -1f);

            var oldRail = panel.Find("IndustrialAccentRail");
            if (oldRail != null)
            {
                oldRail.gameObject.SetActive(false);
            }
        }

        private static void StyleQgcButton(Button button)
        {
            if (button == null || button.name == "CommandCenterMissionConfigButton")
            {
                return;
            }

            var isDrawerTab = button.name.StartsWith("Hud", System.StringComparison.Ordinal) && button.name.EndsWith("Tab", System.StringComparison.Ordinal);
            var isPrimary = button.name == "PlayPauseButton"
                || button.name == "LoadCsvButton"
                || button.name == "SimulationApplyButton"
                || button.name == "PredictionToggleButton"
                || button.name == "OceanCurrentLookupButton"
                || button.name == "OceanCurrentDrawerLookupButton"
                || button.name == "CameraResetCommand";
            var normal = isPrimary ? QgcAccent : QgcControl;
            var buttonImage = button.image ?? button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (!isDrawerTab && buttonImage != null)
            {
                buttonImage.color = Color.white;
            }

            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = isPrimary ? QgcAccentBright : new Color(0.27f, 0.31f, 0.34f, 1f);
            colors.pressedColor = isPrimary ? new Color(0.08f, 0.4f, 0.58f, 1f) : new Color(0.1f, 0.12f, 0.13f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.effectColor = isPrimary ? QgcAccentBright : QgcBorder;
            outline.effectDistance = new Vector2(1f, -1f);

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = Color.white;
                label.fontStyle = FontStyle.Bold;
            }

        }

        private static void StyleQgcInput(InputField input)
        {
            if (input == null)
            {
                return;
            }

            var image = input.targetGraphic as Image;
            if (image != null)
            {
                image.color = new Color(0.07f, 0.085f, 0.095f, 0.99f);
            }

            var outline = input.GetComponent<Outline>() ?? input.gameObject.AddComponent<Outline>();
            outline.effectColor = QgcBorder;
            outline.effectDistance = new Vector2(1f, -1f);
            input.selectionColor = new Color(QgcAccent.r, QgcAccent.g, QgcAccent.b, 0.55f);
            if (input.textComponent != null)
            {
                input.textComponent.color = Color.white;
            }

            var placeholder = input.placeholder as Text;
            if (placeholder != null)
            {
                placeholder.color = QgcTextDim;
            }

        }

        private static void StyleQgcToggle(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            if (toggle.targetGraphic is Image background)
            {
                background.color = new Color(0.07f, 0.085f, 0.095f, 1f);
                var outline = background.GetComponent<Outline>() ?? background.gameObject.AddComponent<Outline>();
                outline.effectColor = QgcBorder;
                outline.effectDistance = new Vector2(1f, -1f);
            }

            if (toggle.graphic is Image checkmark)
            {
                checkmark.color = QgcAccentBright;
            }
        }

        private static void StyleQgcSlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            var background = slider.transform.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.065f, 0.08f, 0.09f, 1f);
            }

            var fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (fill != null)
            {
                fill.color = QgcAccent;
            }

            var handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (handle != null)
            {
                handle.color = new Color(0.88f, 0.92f, 0.94f, 1f);
            }
        }

        private DrawerBinding FindDrawer(string drawerId)
        {
            foreach (var drawer in drawers)
            {
                if (drawer.Id == drawerId)
                {
                    return drawer;
                }
            }

            return null;
        }

        private RectTransform FindRect(string objectName)
        {
            if (runtimeCanvas == null)
            {
                return null;
            }

            foreach (var rect in runtimeCanvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name == objectName)
                {
                    return rect;
                }
            }

            return null;
        }

        private void ConfigureHeaderText(string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size,
            int fontSize, TextAnchor alignment, FontStyle fontStyle, Color color)
        {
            var rect = FindRect(objectName);
            if (rect != null)
            {
                SetFixedRect(rect, anchorMin, anchorMax, pivot, position, size);
                var text = rect.GetComponent<Text>();
                if (text != null)
                {
                    text.fontSize = fontSize;
                    text.alignment = alignment;
                    text.fontStyle = fontStyle;
                    text.color = color;
                }
            }
        }

        private Vector2 GetCanvasSize()
        {
            var canvasRect = runtimeCanvas != null ? runtimeCanvas.transform as RectTransform : null;
            if (canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f)
            {
                return canvasRect.rect.size;
            }

            return new Vector2(1920f, 1080f);
        }

        private void BringTabsToFront()
        {
            if (tabLayer != null)
            {
                tabLayer.SetAsLastSibling();
            }
        }

        private void RemoveExistingTabLayer()
        {
            var existing = FindRect("HudDrawerTabLayer");
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        private static void SetPanelOpacity(RectTransform panel, float alpha)
        {
            var image = panel != null ? panel.GetComponent<Image>() : null;
            if (image == null)
            {
                return;
            }

            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static void SetFixedRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
