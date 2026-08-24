using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public enum UiVisualRole
    {
        PanelFill,
        CardFill,
        ControlFill,
        PanelEdge,
        Divider,
        ButtonFill,
        AccentFill,
        AccentText,
        InputFill,
        Text,
        MutedText,
        AuxiliaryText,
        SuccessFill,
        WarningFill,
        ErrorFill
    }

    public static class UiFactory
    {
        public static readonly Color CommandPageBackground = new Color(0.027451f, 0.094118f, 0.129412f, 1f);
        public static readonly Color CommandPanelFill = new Color(0.043137f, 0.141176f, 0.188235f, 1f);
        public static readonly Color CommandCardFill = new Color(0.062745f, 0.184314f, 0.235294f, 1f);
        public static readonly Color CommandControlFill = new Color(0.082353f, 0.231373f, 0.290196f, 1f);
        public static readonly Color CommandPanelEdge = new Color(0.086275f, 0.266667f, 0.329412f, 1f);
        public static readonly Color CommandDivider = new Color(0.086275f, 0.266667f, 0.329412f, 1f);
        public static readonly Color CommandAccent = new Color(0.3647059f, 0.84313726f, 0.9098039f, 1f);
        public static readonly Color CommandText = new Color(0.898039f, 0.949020f, 0.952941f, 1f);
        public static readonly Color CommandButtonFill = new Color(0.082353f, 0.231373f, 0.290196f, 1f);
        public static readonly Color CommandInputFill = new Color(0.027451f, 0.094118f, 0.129412f, 1f);
        public static readonly Color CommandViewportOverlay = new Color(0.027451f, 0.094118f, 0.129412f, 0.20f);
        public static readonly Color CommandMutedText = new Color(0.568627f, 0.709804f, 0.745098f, 1f);
        public static readonly Color CommandSuccess = new Color(0.254902f, 0.776471f, 0.654902f, 1f);
        public static readonly Color CommandWarning = new Color(0.905f, 0.788f, 0.419f, 1f);
        public static readonly Color CommandError = new Color(0.949020f, 0.482353f, 0.482353f, 1f);
        public static readonly Color CommandDarkText = new Color(0.031f, 0.090f, 0.129f, 1f);
        public const float CommandCenterHeaderHeight = 48f;
        public const float CommandCenterParameterBarHeight = 48f;
        public const float CommandCenterConfigurationAreaHeight = 176f;
        public const float CommandCenterPlaybackBarHeight = 92f;
        public const float CommandCenterMainBodyBottomOffset = CommandCenterConfigurationAreaHeight + CommandCenterPlaybackBarHeight;
        public const float CommandCenterContentTopOffset = CommandCenterHeaderHeight + CommandCenterParameterBarHeight + 16f;
        public const float CommandCenterOperationsTopOffset = 142f;
        public const float MinimumControlHeight = 32f;
        public const float MinimumDrawerToggleHeight = 36f;
        public const float FixedValueColumnWidth = 112f;
        public const float FixedUnitColumnWidth = 52f;
        private static Font font;

        public static Canvas EnsureCanvas(Transform parent)
        {
            var parentCanvas = parent != null
                ? (parent.GetComponent<Canvas>() ?? parent.GetComponentInParent<Canvas>())
                : null;
            if (parentCanvas != null)
            {
                RuntimeUiFallback.RememberLegacyCanvas(parentCanvas);
                return parentCanvas;
            }

            if (RuntimeUiFallback.LegacyCanvas != null)
            {
                return EnsureCanvas(parent, parent != null ? parent.name : "UnknownPanel", RuntimeUiFallback.LegacyCanvas);
            }

            var canvas = EnsureCanvas(parent, parent != null ? parent.name : "UnknownPanel", null);
            RuntimeUiFallback.RememberLegacyCanvas(canvas);
            return canvas;
        }

        public static Canvas EnsureCanvas(Transform parent, string fallbackPanelName, Canvas explicitFallbackCanvas = null)
        {
            EnsureEventSystem();
            if (explicitFallbackCanvas != null)
            {
                return explicitFallbackCanvas;
            }

            var parentCanvas = parent != null
                ? (parent.GetComponent<Canvas>() ?? parent.GetComponentInParent<Canvas>())
                : null;
            if (parentCanvas != null)
            {
                return parentCanvas;
            }

            if (!RuntimeUiFallback.AllowRuntimeFallback)
            {
                throw new System.InvalidOperationException($"Runtime UI fallback is disabled. Missing RuntimeCanvas for {fallbackPanelName}.");
            }

            RuntimeUiFallback.LogFallback(fallbackPanelName);
            var canvasObject = new GameObject("RuntimeCanvas");
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            RuntimeUiFallback.RememberLegacyCanvas(canvas);
            EnsureResponsiveRuntimeLayout(canvas);
            return canvas;
        }

        public static RectTransform EnsureResponsiveRuntimeLayout(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            var uiRoot = EnsureRectTransformChild(canvas.transform, "UiRoot");
            ConfigureRect(uiRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ConfigureRootLayout(uiRoot);

            var systemBar = EnsureRectTransformChild(uiRoot, "SystemBar");
            ConfigureRect(systemBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, CommandCenterHeaderHeight));
            ConfigureZoneHeight(systemBar, CommandCenterHeaderHeight);
            var configurationArea = EnsureRectTransformChild(uiRoot, "ConfigurationArea");
            ConfigureRect(configurationArea, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, CommandCenterPlaybackBarHeight), new Vector2(0f, CommandCenterConfigurationAreaHeight));
            ConfigureVerticalContent(configurationArea, 12f);
            ConfigureZoneHeight(configurationArea, CommandCenterConfigurationAreaHeight);
            var playbackBar = EnsureRectTransformChild(uiRoot, "PlaybackBar");
            ConfigureRect(playbackBar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, CommandCenterPlaybackBarHeight));
            ConfigureVerticalContent(playbackBar, 12f);
            ConfigureZoneHeight(playbackBar, CommandCenterPlaybackBarHeight);
            var mainBody = EnsureRectTransformChild(uiRoot, "MainBody");
            ConfigureRect(mainBody, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            mainBody.offsetMin = new Vector2(0f, CommandCenterMainBodyBottomOffset);
            mainBody.offsetMax = new Vector2(0f, -48f);
            ConfigureMainBody(mainBody);

            var telemetryColumn = EnsureRectTransformChild(mainBody, "TelemetryColumn");
            ConfigureRect(telemetryColumn, new Vector2(0f, 0f), new Vector2(0.25f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            ConfigureVerticalContent(telemetryColumn, 12f);
            ConfigureHorizontalZone(telemetryColumn, 280f, 280f);
            var viewportColumn = EnsureRectTransformChild(mainBody, "ViewportColumn");
            ConfigureRect(viewportColumn, new Vector2(0.25f, 0f), new Vector2(0.75f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ConfigureVerticalContent(viewportColumn, 12f);
            ConfigureHorizontalZone(viewportColumn, 640f, 0f);
            viewportColumn.GetComponent<LayoutElement>().flexibleWidth = 1f;
            EnsureViewportSurface(viewportColumn, null);
            var statusColumn = EnsureRectTransformChild(mainBody, "StatusColumn");
            ConfigureRect(statusColumn, new Vector2(0.75f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            ConfigureVerticalContent(statusColumn, 12f);
            ConfigureHorizontalZone(statusColumn, 320f, 320f);

            var drawerEntryLayer = EnsureRectTransformChild(uiRoot, "DrawerEntryLayer");
            ConfigureRect(drawerEntryLayer, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var drawerLayout = drawerEntryLayer.GetComponent<LayoutElement>() ?? drawerEntryLayer.gameObject.AddComponent<LayoutElement>();
            drawerLayout.ignoreLayout = true;
            EnsureRuntimeDrawerToggle(drawerEntryLayer, "TelemetryDrawerToggle", "遥测抽屉", new Vector2(16f, -12f));
            EnsureRuntimeDrawerToggle(drawerEntryLayer, "StatusDrawerToggle", "状态抽屉", new Vector2(148f, -12f));

            var modalRoot = EnsureRectTransformChild(canvas.transform, "ModalRoot");
            ConfigureRect(modalRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var scrim = EnsureImageChild(modalRoot, "DrawerScrim", new Color(0f, 0f, 0f, 0.6f));
            scrim.raycastTarget = true;
            EnsureRuntimeDrawerPlaceholder(modalRoot, "OceanCurrentDrawer");
            EnsureRuntimeDrawerPlaceholder(modalRoot, "FlightLegDrawer");
            EnsureTooltipPopup(modalRoot);
            return uiRoot;
        }

        public static ViewportSurfaceController EnsureViewportSurface(RectTransform viewportColumn, Camera sourceCamera)
        {
            if (viewportColumn == null)
            {
                return null;
            }

            var host = viewportColumn.Find("ViewportSurfaceHost") as RectTransform;
            if (host == null)
            {
                host = EnsureRectTransformChild(viewportColumn, "ViewportSurfaceHost");
            }

            ConfigureRect(host, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var hostLayout = host.GetComponent<LayoutElement>() ?? host.gameObject.AddComponent<LayoutElement>();
            hostLayout.ignoreLayout = true;
            host.SetAsFirstSibling();

            var surfaceTransform = host.Find("ViewportSurface") as RectTransform;
            if (surfaceTransform == null)
            {
                surfaceTransform = EnsureRectTransformChild(host, "ViewportSurface");
            }

            ConfigureRect(surfaceTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var surface = surfaceTransform.GetComponent<RawImage>() ?? surfaceTransform.gameObject.AddComponent<RawImage>();
            surface.raycastTarget = false;
            surface.color = Color.white;
            surfaceTransform.SetAsFirstSibling();

            var canvas = viewportColumn.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var legacyFrame = FindDescendant(canvas.transform, "OceanViewportFrame") as RectTransform;
                if (legacyFrame != null)
                {
                    if (legacyFrame.parent != viewportColumn)
                    {
                        legacyFrame.SetParent(viewportColumn, false);
                    }

                    ConfigureRect(legacyFrame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    var legacyLayout = legacyFrame.GetComponent<LayoutElement>() ?? legacyFrame.gameObject.AddComponent<LayoutElement>();
                    legacyLayout.ignoreLayout = true;
                    var legacyImage = legacyFrame.GetComponent<Image>();
                    if (legacyImage != null)
                    {
                        legacyImage.raycastTarget = false;
                    }
                    legacyFrame.SetSiblingIndex(Mathf.Min(1, viewportColumn.childCount - 1));
                }

                var toolbar = FindDescendant(canvas.transform, "OceanCommandToolbar") as RectTransform;
                if (toolbar != null)
                {
                    if (toolbar.parent != viewportColumn)
                    {
                        toolbar.SetParent(viewportColumn, false);
                    }

                    toolbar.SetAsLastSibling();
                }
            }

            var controller = host.GetComponent<ViewportSurfaceController>() ?? host.gameObject.AddComponent<ViewportSurfaceController>();
            if (sourceCamera != null || controller.Host == null || controller.Surface != surface)
            {
                controller.Bind(sourceCamera, host, surface);
            }
            return controller;
        }

        public static RectTransform Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            parent = ResolveRuntimePanelParent(parent, name);
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, size);
            var image = panel.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        public static RectTransform CommandPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var panel = Panel(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, size, CommandPanelFill);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = CommandPanelEdge;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            return panel;
        }

        public static RectTransform EnsureCommandCenterHeader(Transform parent)
        {
            var canvas = parent != null
                ? (parent.GetComponent<Canvas>() ?? parent.GetComponentInParent<Canvas>())
                : null;
            var existing = canvas != null
                ? (canvas.transform.Find("UiRoot/SystemBar/CommandCenterHeader") as RectTransform
                    ?? canvas.transform.Find("CommandCenterHeader") as RectTransform)
                : null;
            if (existing != null)
            {
                EnsureHeaderExit(existing);
                NormalizeCommandCenterHeader(existing);
                return existing;
            }

            var header = Panel(
                "CommandCenterHeader",
                canvas != null && canvas.transform.Find("UiRoot/SystemBar") != null ? canvas.transform.Find("UiRoot/SystemBar") : parent,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                new Color(0.027f, 0.071f, 0.110f, 0.98f));
            var layout = header.gameObject.GetComponent<LayoutElement>() ?? header.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = CommandCenterHeaderHeight;
            layout.preferredHeight = CommandCenterHeaderHeight;
            var outline = header.gameObject.AddComponent<Outline>();
            outline.effectColor = CommandPanelEdge;
            outline.effectDistance = new Vector2(0f, -1f);
            Text("CommandCenterProductName", header, "UnderwaterGliderTwin", 18, TextAnchor.MiddleLeft, CommandText,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(320f, 32f));
            Text("CommandCenterSystemHealth", header, "系统正常", 14, TextAnchor.MiddleRight, CommandSuccess,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-176f, 0f), new Vector2(120f, 28f));
            Text("CommandCenterRuntime", header, "海流任务指挥舱", 14, TextAnchor.MiddleRight, CommandMutedText,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(142f, 28f));
            Text("CommandCenterExit", header, "退出", 14, TextAnchor.MiddleRight, CommandMutedText,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-188f, -18f), new Vector2(52f, 20f));
            NormalizeCommandCenterHeader(header);
            return header;
        }

        private static void NormalizeCommandCenterHeader(RectTransform header)
        {
            if (header == null)
            {
                return;
            }

            var product = header.Find("CommandCenterProductName") as RectTransform;
            if (product != null)
            {
                ConfigureRect(product, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(22f, 0f), new Vector2(320f, 32f));
                var productText = product.GetComponent<Text>();
                if (productText != null)
                {
                    productText.alignment = TextAnchor.MiddleLeft;
                }
            }
            NormalizeHeaderText(header.Find("CommandCenterSystemHealth") as RectTransform, new Vector2(-234f, 0f), new Vector2(120f, 28f), TextAnchor.MiddleRight);
            NormalizeHeaderText(header.Find("CommandCenterRuntime") as RectTransform, new Vector2(-80f, 0f), new Vector2(142f, 28f), TextAnchor.MiddleRight);
            NormalizeHeaderText(header.Find("CommandCenterExit") as RectTransform, new Vector2(-16f, 0f), new Vector2(52f, 28f), TextAnchor.MiddleRight);
        }

        private static void NormalizeHeaderText(RectTransform rect, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
        {
            if (rect == null)
            {
                return;
            }

            ConfigureRect(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), anchoredPosition, size);
            var text = rect.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = alignment;
            }
        }

        private static void EnsureHeaderExit(RectTransform header)
        {
            if (header == null || header.Find("CommandCenterExit") != null)
            {
                return;
            }

            Text("CommandCenterExit", header, "退出", 14, TextAnchor.MiddleRight, CommandMutedText,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-188f, -18f), new Vector2(52f, 20f));
        }

        public static Text Text(string name, Transform parent, string value, int fontSize, TextAnchor anchor, Color color, Vector2 anchoredPosition, Vector2 size)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            ConfigureRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
            var text = textObject.AddComponent<Text>();
            text.font = GetFont();
            text.text = value;
            text.fontSize = Mathf.Max(fontSize, 10);
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public static Text Text(string name, Transform parent, string value, int fontSize, TextAnchor anchor, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var text = Text(name, parent, value, fontSize, anchor, color, anchoredPosition, size);
            ConfigureRect(text.rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, size);
            return text;
        }

        public static Button Button(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
        {
            return Button(name, parent, label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
        }

        public static Button Button(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, ClampControlSize(size, MinimumControlHeight));
            var image = buttonObject.AddComponent<Image>();
            ApplyCommandPalette(image, UiVisualRole.ButtonFill);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = CommandPanelEdge;
            outline.effectDistance = new Vector2(1f, -1f);
            var button = buttonObject.AddComponent<Button>();
            button.colors = CreateButtonStates();

            var labelText = Text(name + "Label", buttonObject.transform, label, 16, TextAnchor.MiddleCenter, CommandText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, rect.sizeDelta);
            labelText.raycastTarget = false;
            return button;
        }

        public static Button PrimaryButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var button = Button(name, parent, label, anchorMin, anchorMax, pivot, anchoredPosition, size);
            ApplyCommandPalette(button.image, UiVisualRole.AccentFill);
            var outline = button.GetComponent<Outline>();
            outline.effectColor = CommandAccent;
            button.colors = CreateButtonStates();
            var labelText = button.GetComponentInChildren<Text>();
            if (labelText != null)
            {
                ApplyCommandPalette(labelText, UiVisualRole.AccentText);
            }
            return button;
        }

        public static Image ProgressBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var root = Panel(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, size, new Color(0.05f, 0.16f, 0.18f, 0.95f));
            var fill = Panel(name + "Fill", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(size.x, 0f), new Color(0.07f, 0.72f, 0.9f, 0.95f));
            return fill.GetComponent<Image>();
        }

        public static Toggle Toggle(string name, Transform parent, string label, bool isOn, Vector2 anchoredPosition, Vector2 size)
        {
            return Toggle(name, parent, label, isOn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
        }

        public static Toggle Toggle(string name, Transform parent, string label, bool isOn, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var toggleObject = new GameObject(name);
            toggleObject.transform.SetParent(parent, false);
            var rect = toggleObject.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, ClampControlSize(size, MinimumControlHeight));

            var background = new GameObject("Background");
            background.transform.SetParent(toggleObject.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(10f, 0f);
            backgroundRect.sizeDelta = new Vector2(18f, 18f);
            ApplyCommandPalette(background.AddComponent<Image>(), UiVisualRole.InputFill);

            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            var checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = new Vector2(-5f, -5f);
            ApplyCommandPalette(checkmark.AddComponent<Image>(), UiVisualRole.AccentFill);

            var labelText = Text(name + "Label", toggleObject.transform, label, 14, TextAnchor.MiddleLeft, CommandText, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(132f, 24f));
            labelText.raycastTarget = false;

            var toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = isOn;
            toggle.colors = CreateButtonStates();
            return toggle;
        }

        public static Slider Slider(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            return Slider(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);
        }

        public static Slider Slider(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var sliderObject = new GameObject(name);
            sliderObject.transform.SetParent(parent, false);
            var rect = sliderObject.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, ClampControlSize(size, MinimumControlHeight));

            var background = Panel("Background", sliderObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, CommandInputFill);
            background.offsetMin = new Vector2(0f, 8f);
            background.offsetMax = new Vector2(0f, -8f);

            var fillArea = Panel("Fill Area", sliderObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear);
            fillArea.offsetMin = new Vector2(4f, 8f);
            fillArea.offsetMax = new Vector2(-4f, -8f);

            var fill = Panel("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, CommandAccent);

            var handleArea = Panel("Handle Slide Area", sliderObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear);
            var handle = Panel("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 24f), CommandText);

            var slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        public static InputField InputField(string name, Transform parent, string value, string placeholder, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);
            var rect = inputObject.AddComponent<RectTransform>();
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, ClampControlSize(size, MinimumControlHeight));
            var image = inputObject.AddComponent<Image>();
            ApplyCommandPalette(image, UiVisualRole.InputFill);
            var outline = inputObject.AddComponent<Outline>();
            outline.effectColor = CommandPanelEdge;
            outline.effectDistance = new Vector2(1f, -1f);

            var text = Text(name + "Text", inputObject.transform, value, 14, TextAnchor.MiddleLeft, CommandText, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(-20f, -10f));
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;

            var placeholderText = Text(name + "Placeholder", inputObject.transform, placeholder, 14, TextAnchor.MiddleLeft, new Color(CommandMutedText.r, CommandMutedText.g, CommandMutedText.b, 0.58f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(-20f, -10f));
            placeholderText.raycastTarget = false;

            var inputField = inputObject.AddComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            inputField.caretWidth = 2;
            inputField.text = value;
            inputField.colors = CreateButtonStates();
            return inputField;
        }

        public static void ConfigureVerticalContent(RectTransform content, float spacing)
        {
            if (content == null)
            {
                return;
            }

            var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        public static void ConfigureHorizontalZone(RectTransform zone, float minWidth, float preferredWidth)
        {
            if (zone == null)
            {
                return;
            }

            var layoutElement = zone.GetComponent<LayoutElement>() ?? zone.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = minWidth;
            layoutElement.preferredWidth = preferredWidth;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 1f;
        }

        public static void ApplyCommandPalette(Graphic graphic, UiVisualRole role)
        {
            if (graphic == null)
            {
                return;
            }

            switch (role)
            {
                case UiVisualRole.PanelFill:
                    graphic.color = CommandPanelFill;
                    break;
                case UiVisualRole.CardFill:
                    graphic.color = CommandCardFill;
                    break;
                case UiVisualRole.ControlFill:
                    graphic.color = CommandControlFill;
                    break;
                case UiVisualRole.PanelEdge:
                    graphic.color = CommandPanelEdge;
                    break;
                case UiVisualRole.Divider:
                    graphic.color = CommandDivider;
                    break;
                case UiVisualRole.ButtonFill:
                    graphic.color = CommandButtonFill;
                    break;
                case UiVisualRole.AccentFill:
                    graphic.color = CommandAccent;
                    break;
                case UiVisualRole.AccentText:
                    graphic.color = CommandDarkText;
                    break;
                case UiVisualRole.InputFill:
                    graphic.color = CommandInputFill;
                    break;
                case UiVisualRole.Text:
                    graphic.color = CommandText;
                    break;
                case UiVisualRole.MutedText:
                    graphic.color = CommandMutedText;
                    break;
                case UiVisualRole.AuxiliaryText:
                    graphic.color = CommandMutedText;
                    break;
                case UiVisualRole.SuccessFill:
                    graphic.color = CommandSuccess;
                    break;
                case UiVisualRole.WarningFill:
                    graphic.color = CommandWarning;
                    break;
                case UiVisualRole.ErrorFill:
                    graphic.color = CommandError;
                    break;
            }
        }

        private static Font GetFont()
        {
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Noto Sans SC", "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" },
                    32);
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }

            return font;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static Transform ResolveRuntimePanelParent(Transform parent, string name)
        {
            if (!RuntimeUiFallback.AllowRuntimeFallback || parent == null)
            {
                return parent;
            }

            var canvas = parent.GetComponent<Canvas>();
            if (canvas == null)
            {
                return parent;
            }

            // Auxiliary modal canvases (for example the legacy ocean-current
            // editor) are already isolated overlays. Do not scaffold the
            // command-center hierarchy inside them; that would create another
            // UiRoot/ModalRoot pair and break the single-modal-owner rule.
            if (RuntimeUiFallback.LegacyCanvas != null && canvas != RuntimeUiFallback.LegacyCanvas)
            {
                return parent;
            }

            var uiRoot = EnsureResponsiveRuntimeLayout(canvas);
            switch (name)
            {
                case "FlightLegDrawerPanel":
                    return canvas.transform.Find("ModalRoot") ?? parent;
                case "OceanViewportFrame":
                case "OceanCommandToolbar":
                    return uiRoot != null ? uiRoot.Find("MainBody/ViewportColumn") ?? parent : parent;
                default: return parent;
            }
        }

        private static RectTransform EnsureRectTransformChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing as RectTransform ?? existing.gameObject.AddComponent<RectTransform>();
            }

            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created.GetComponent<RectTransform>();
        }

        private static Image EnsureImageChild(Transform parent, string name, Color color)
        {
            var existing = parent.Find(name);
            var image = existing != null ? existing.GetComponent<Image>() : null;
            if (image != null)
            {
                return image;
            }

            var created = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            var rect = created.GetComponent<RectTransform>();
            ConfigureRect(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            image = created.GetComponent<Image>() ?? created.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static UiTooltipController EnsureTooltipPopup(RectTransform modalRoot)
        {
            if (modalRoot == null)
            {
                return null;
            }

            var popup = EnsureRectTransformChild(modalRoot, "TooltipPopup");
            ConfigureRect(popup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 52f));
            var image = popup.GetComponent<Image>();
            if (image == null)
            {
                image = popup.gameObject.AddComponent<Image>();
            }
            image.color = CommandPanelFill;
            image.raycastTarget = false;
            var group = popup.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = popup.gameObject.AddComponent<CanvasGroup>();
            }
            group.interactable = false;
            group.blocksRaycasts = false;

            var message = popup.Find("Message")?.GetComponent<Text>();
            if (message == null)
            {
                message = Text("Message", popup, string.Empty, 14, TextAnchor.MiddleLeft, CommandText,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(12f, 4f), new Vector2(-24f, -8f));
            }
            message.raycastTarget = false;
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Overflow;

            var controller = popup.GetComponent<UiTooltipController>() ?? popup.gameObject.AddComponent<UiTooltipController>();
            controller.ConfigurePopup(popup, image, group, message);
            popup.gameObject.SetActive(false);
            return controller;
        }

        public static UiFocusVisual EnsureFocusVisual(Selectable selectable)
        {
            if (selectable == null)
            {
                return null;
            }

            var visual = selectable.GetComponent<UiFocusVisual>() ?? selectable.gameObject.AddComponent<UiFocusVisual>();
            visual.SetHost(selectable);
            return visual;
        }

        public static UiTooltip EnsureTooltip(Selectable selectable, UiTooltipController controller, string message)
        {
            if (selectable == null)
            {
                return null;
            }

            var tooltip = selectable.GetComponent<UiTooltip>() ?? selectable.gameObject.AddComponent<UiTooltip>();
            tooltip.SetHost(selectable);
            tooltip.SetController(controller);
            tooltip.SetMessage(message);
            return tooltip;
        }

        public static void ApplyAccessibleFeedback(Transform root, UiTooltipController controller = null)
        {
            if (root == null)
            {
                return;
            }

            var selectables = root.GetComponentsInChildren<Selectable>(true);
            for (var i = 0; i < selectables.Length; i++)
            {
                var selectable = selectables[i];
                if (selectable == null)
                {
                    continue;
                }

                EnsureFocusVisual(selectable);
                if (controller != null && (selectable is Button || selectable is Toggle))
                {
                    EnsureTooltip(selectable, controller, BuildTooltipMessage(selectable));
                }
            }

        }

        private static string BuildTooltipMessage(Selectable selectable)
        {
            var name = selectable != null ? selectable.name : string.Empty;
            if (name.IndexOf("DrawerToggle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return name.IndexOf("Telemetry", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? "打开遥测抽屉"
                    : "打开状态抽屉";
            }

            return name;
        }

        private static void EnsureRuntimeDrawerToggle(Transform parent, string name, string label, Vector2 position)
        {
            var existing = parent.Find(name);
            var button = existing != null ? existing.GetComponent<Button>() : null;
            if (button == null)
            {
                button = Button(name, parent, label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(120f, MinimumDrawerToggleHeight));
            }

            ConfigureRect(button.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(120f, MinimumDrawerToggleHeight));
        }

        private static void EnsureRuntimeDrawerPlaceholder(Transform parent, string name)
        {
            var placeholder = EnsureRectTransformChild(parent, name);
            ConfigureRect(placeholder, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(420f, 0f));
            placeholder.gameObject.SetActive(false);
        }

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static Vector2 ClampControlSize(Vector2 size, float minimumHeight)
        {
            return new Vector2(size.x, Mathf.Max(size.y, minimumHeight));
        }

        private static void ConfigureRootLayout(RectTransform uiRoot)
        {
            var layout = uiRoot.GetComponent<VerticalLayoutGroup>() ?? uiRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.enabled = false;
        }

        private static void ConfigureZoneHeight(RectTransform zone, float height)
        {
            var layoutElement = zone.GetComponent<LayoutElement>() ?? zone.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
        }

        private static void ConfigureMainBody(RectTransform mainBody)
        {
            var layout = mainBody.GetComponent<HorizontalLayoutGroup>() ?? mainBody.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var element = mainBody.GetComponent<LayoutElement>() ?? mainBody.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;
        }

        private static ColorBlock CreateButtonStates()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(0.86f, 0.96f, 0.98f, 1f);
            colors.pressedColor = new Color(0.70f, 0.86f, 0.90f, 1f);
            colors.selectedColor = new Color(0.78f, 0.96f, 0.98f, 1f);
            colors.disabledColor = new Color(0.48f, 0.58f, 0.62f, 0.52f);
            colors.fadeDuration = 0.12f;
            return colors;
        }

        private static bool IsDefaultGraphicColor(Color color)
        {
            return color == Color.white || color.a <= 0.001f;
        }

        private static bool IsDefaultButtonStates(ColorBlock colors)
        {
            return AreButtonStatesEqual(colors, ColorBlock.defaultColorBlock)
                || AreButtonStatesEqual(colors, CreateButtonStates());
        }

        private static bool AreButtonStatesEqual(ColorBlock left, ColorBlock right)
        {
            return left.normalColor == right.normalColor
                && left.highlightedColor == right.highlightedColor
                && left.pressedColor == right.pressedColor
                && left.selectedColor == right.selectedColor
                && left.disabledColor == right.disabledColor
                && left.colorMultiplier == right.colorMultiplier
                && left.fadeDuration == right.fadeDuration;
        }

        private static bool IsPrimaryButton(Button button)
        {
            if (button == null)
            {
                return false;
            }

            var name = button.name ?? string.Empty;
            return name.IndexOf("Apply", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("PlayPause", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("ResetCommand", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ApplyTextRoleColor(Text text, UiTextRole role)
        {
            switch (role)
            {
                case UiTextRole.Title:
                case UiTextRole.Value:
                case UiTextRole.Label:
                    ApplyCommandPalette(text, UiVisualRole.Text);
                    break;
                case UiTextRole.SectionTitle:
                case UiTextRole.Auxiliary:
                    ApplyCommandPalette(text, UiVisualRole.MutedText);
                    break;
                case UiTextRole.Button:
                    ApplyCommandPalette(text, text.GetComponentInParent<Button>() != null && IsPrimaryButton(text.GetComponentInParent<Button>())
                        ? UiVisualRole.AccentText
                        : UiVisualRole.Text);
                    break;
                case UiTextRole.Error:
                    ApplyCommandPalette(text, UiVisualRole.ErrorFill);
                    break;
            }
        }

        public static void SetButtonText(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        public static void ApplyResponsivePanelRoots(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
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
                var panel = FindDescendant(canvas.transform, panelName) as RectTransform;
                if (panel == null)
                {
                    continue;
                }

                var verticalInset = panelName == "PlaybackControlsPanel" ? 0f : -24f;
                ConfigureRect(panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, verticalInset));
                var layout = panel.GetComponent<LayoutElement>() ?? panel.gameObject.AddComponent<LayoutElement>();
                layout.minWidth = 0f;
                layout.preferredWidth = 0f;
                layout.flexibleWidth = 1f;
                layout.minHeight = 0f;
                layout.preferredHeight = 0f;
                layout.flexibleHeight = 1f;
            }
        }

        public static void ApplyRuntimePalette(Transform root)
        {
            if (root == null)
            {
                return;
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
                var panel = FindDescendant(root, panelName);
                if (panel == null)
                {
                    continue;
                }

                var panelImage = panel.GetComponent<Image>();
                var panelWasDefault = IsDefaultGraphicColor(panelImage != null ? panelImage.color : Color.clear);
                if (panelWasDefault)
                {
                    ApplyCommandPalette(panelImage, UiVisualRole.PanelFill);
                }
                if (panelName == "OceanCommandToolbar" && panelImage != null)
                {
                    if (panelWasDefault || panelImage.color == CommandPanelFill)
                    {
                        panelImage.color = CommandViewportOverlay;
                        panelImage.raycastTarget = false;
                    }
                }
                foreach (var outline in panel.GetComponents<Outline>())
                {
                    if (outline.effectColor == Color.white || outline.effectColor == CommandPanelEdge)
                    {
                        outline.effectColor = CommandPanelEdge;
                    }
                }

                foreach (var button in panel.GetComponentsInChildren<Button>(true))
                {
                    if (button.image != null && IsDefaultGraphicColor(button.image.color))
                    {
                        ApplyCommandPalette(button.image, IsPrimaryButton(button) ? UiVisualRole.AccentFill : UiVisualRole.ButtonFill);
                    }
                    if (IsDefaultButtonStates(button.colors))
                    {
                        button.colors = CreateButtonStates();
                    }
                    foreach (var outline in button.GetComponents<Outline>())
                    {
                        if (outline.effectColor == Color.white || outline.effectColor == CommandPanelEdge)
                        {
                            outline.effectColor = CommandPanelEdge;
                        }
                    }
                }

                foreach (var input in panel.GetComponentsInChildren<InputField>(true))
                {
                    var inputImage = input.GetComponent<Image>();
                    if (IsDefaultGraphicColor(inputImage != null ? inputImage.color : Color.clear))
                    {
                        ApplyCommandPalette(inputImage, UiVisualRole.InputFill);
                    }
                    foreach (var outline in input.GetComponents<Outline>())
                    {
                        if (outline.effectColor == Color.white || outline.effectColor == CommandPanelEdge)
                        {
                            outline.effectColor = CommandPanelEdge;
                        }
                    }
                }

                foreach (var text in panel.GetComponentsInChildren<Text>(true))
                {
                    if (IsDefaultGraphicColor(text.color))
                    {
                        ApplyTextRoleColor(text, ResolveTextRole(text));
                    }
                }
            }
        }

        public static void ApplyTextRole(Text text, UiTextRole role, RuntimeUiLayoutMode mode)
        {
            if (text == null)
            {
                return;
            }

            var profile = ResponsiveUiTypography.ForMode(mode, 0f, 0f);
            text.fontSize = Mathf.Max(text.fontSize, ResponsiveUiTypography.GetLogicalSize(profile, role));
            ApplyTextRoleColor(text, role);
        }

        public static void ConfigureFixedLabelColumn(Text text, float width = 116f)
        {
            if (text == null)
            {
                return;
            }

            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            var layout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            ApplyTextRole(text, UiTextRole.Label, RuntimeUiLayoutMode.CompressedThreeColumn);
        }

        public static void ConfigureFixedValueColumn(Text text, float width = FixedValueColumnWidth)
        {
            if (text == null)
            {
                return;
            }

            text.alignment = TextAnchor.MiddleRight;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            var layout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            ApplyTextRole(text, UiTextRole.Value, RuntimeUiLayoutMode.CompressedThreeColumn);
        }

        public static void ConfigureFixedUnitColumn(Text text, float width = FixedUnitColumnWidth)
        {
            if (text == null)
            {
                return;
            }

            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            var layout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            ApplyTextRole(text, UiTextRole.Auxiliary, RuntimeUiLayoutMode.CompressedThreeColumn);
        }

        public static void ConfigureWrappedStatusText(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            ApplyTextRoleColor(text, ResolveTextRole(text));
            var layout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 18f;
            layout.preferredHeight = 44f;
            layout.flexibleHeight = 0f;
            if (text.rectTransform != null)
            {
                var size = text.rectTransform.sizeDelta;
                size.y = 44f;
                text.rectTransform.sizeDelta = size;
            }
        }

        public static UiTextRole ResolveTextRole(Text text)
        {
            if (text == null)
            {
                return UiTextRole.Value;
            }

            var name = text.name ?? string.Empty;
            if (name.IndexOf("Error", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Alarm", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return UiTextRole.Error;
            }

            if (name.IndexOf("ProductName", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Title", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return UiTextRole.Title;
            }

            if (name.IndexOf("GroupLabel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Legend", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Auxiliary", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return UiTextRole.Auxiliary;
            }

            if (text.GetComponentInParent<Button>() != null)
            {
                return UiTextRole.Button;
            }

            if (name.IndexOf("Label", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return UiTextRole.Label;
            }

            return UiTextRole.Value;
        }

        public static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.effectColor = selected ? CommandAccent : CommandPanelEdge;
            outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        public static void ApplyRuntimeLabels(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                var hasLabel = TryGetRuntimeLabel(text.name, out var label)
                    || TryGetRuntimeLabel(text.text, out label);
                if (!hasLabel && text.GetComponentInParent<Button>() != null && text.transform.parent != null)
                {
                    hasLabel = TryGetRuntimeLabel(text.transform.parent.name, out label);
                }

                if (hasLabel)
                {
                    text.text = label;
                }
            }
        }

        private static bool TryGetRuntimeLabel(string name, out string label)
        {
            switch (name)
            {
                case "DashboardPanel": label = "遥测数据"; return true;
                case "StatusPanel": label = "任务状态"; return true;
                case "DataInputPanel": label = "任务配置"; return true;
                case "PlaybackControlsPanel": label = "播放控制"; return true;
                case "OceanCommandToolbar": label = "三维海流视图"; return true;
                case "CameraFollowCommand":
                case "CameraFollowButton": label = "跟随"; return true;
                case "CameraGlobalCommand":
                case "CameraGlobalButton": label = "全局"; return true;
                case "CameraTopCommand": label = "俯视"; return true;
                case "CameraSideCommand": label = "侧视"; return true;
                case "CameraOrbitCommand":
                case "CameraOrbitButton": label = "环绕"; return true;
                case "CameraResetCommand": label = "复位视角"; return true;
                case "PlayPauseButton": label = "开始"; return true;
                case "ReverseButton": label = "倒放"; return true;
                case "ReplayButton": label = "回放"; return true;
                case "ResetButton": label = "重置"; return true;
                case "ExportButton": label = "导出"; return true;
                case "ExitButton": label = "退出"; return true;
                case "MissionVolumeButton": label = "海域"; return true;
                case "FogToggle": label = "雾效"; return true;
                case "ParticlesToggle": label = "粒子"; return true;
                case "TrajectoryToggle": label = "航迹"; return true;
                case "Speed05Button": label = "0.5x"; return true;
                case "Speed1Button": label = "1x"; return true;
                case "Speed2Button": label = "2x"; return true;
                case "Speed5Button": label = "5x"; return true;
                case "Speed10Button": label = "10x"; return true;
                case "LoadCsvButton": label = "加载 CSV"; return true;
                case "ApplyButton": label = "运行仿真"; return true;
                case "ApplyPredictionConfigButton": label = "应用预测"; return true;
                case "PredictionToggleButton": label = "开始预测"; return true;
                case "FlightLegSettingsButton": label = "航段参数"; return true;
                case "LookupButton": label = "查询海流"; return true;
                case "PreviousLayerButton": label = "上一层"; return true;
                case "NextLayerButton": label = "下一层"; return true;
                case "AddLayerButton": label = "添加层"; return true;
                case "SaveLayerButton": label = "保存层"; return true;
                case "DeleteLayerButton": label = "删除层"; return true;
                case "CsvSourceLabel": label = "CSV 文件"; return true;
                case "ModelLabel": label = "预测模型"; return true;
                case "StatusText": label = "状态"; return true;
                default:
                    label = null;
                    return false;
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
