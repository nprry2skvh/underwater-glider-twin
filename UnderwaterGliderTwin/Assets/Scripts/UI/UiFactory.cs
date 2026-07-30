using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    internal static class UiFactory
    {
        public static readonly Color CommandPanelFill = new Color(0.015f, 0.065f, 0.11f, 0.94f);
        public static readonly Color CommandPanelEdge = new Color(0.08f, 0.68f, 0.92f, 0.62f);
        public static readonly Color CommandAccent = new Color(0.08f, 0.84f, 1f, 1f);
        public static readonly Color CommandText = new Color(0.87f, 0.97f, 1f, 1f);
        public const float CommandCenterHeaderHeight = 48f;
        public const float CommandCenterParameterBarHeight = 48f;
        public const float CommandCenterContentTopOffset = CommandCenterHeaderHeight + CommandCenterParameterBarHeight + 16f;
        public const float CommandCenterOperationsTopOffset = 142f;
        private static Font font;

        public static Canvas EnsureCanvas(Transform parent)
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            EnsureEventSystem();
            if (canvas != null)
            {
                return canvas;
            }

            var canvasObject = new GameObject("RuntimeCanvas");
            canvasObject.transform.SetParent(parent, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
        {
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
            var canvas = Object.FindObjectOfType<Canvas>();
            var existing = canvas != null ? canvas.transform.Find("CommandCenterHeader") as RectTransform : null;
            if (existing != null)
            {
                return existing;
            }

            var header = Panel(
                "CommandCenterHeader",
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, CommandCenterHeaderHeight),
                new Color(0.005f, 0.03f, 0.07f, 0.98f));
            var outline = header.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.06f, 0.62f, 0.95f, 0.72f);
            outline.effectDistance = new Vector2(0f, -1f);
            Text("CommandCenterProductName", header, "UnderwaterGliderTwin", 18, TextAnchor.MiddleLeft, CommandText,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(320f, 32f));
            Text("CommandCenterSystemHealth", header, "系统正常", 12, TextAnchor.MiddleRight, new Color(0.36f, 0.96f, 0.68f, 1f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-176f, 0f), new Vector2(120f, 28f));
            Text("CommandCenterRuntime", header, "海流任务指挥舱", 12, TextAnchor.MiddleRight, new Color(0.55f, 0.82f, 0.94f, 1f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(142f, 28f));
            return header;
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
            text.fontSize = fontSize;
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
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, size);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.035f, 0.19f, 0.29f, 0.96f);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.64f, 0.88f, 0.36f);
            outline.effectDistance = new Vector2(1f, -1f);
            var button = buttonObject.AddComponent<Button>();

            var labelText = Text(name + "Label", buttonObject.transform, label, 14, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            labelText.raycastTarget = false;
            return button;
        }

        public static Button PrimaryButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var button = Button(name, parent, label, anchorMin, anchorMax, pivot, anchoredPosition, size);
            button.image.color = new Color(0.0f, 0.38f, 0.64f, 0.98f);
            var outline = button.GetComponent<Outline>();
            outline.effectColor = new Color(0.10f, 0.90f, 1f, 0.82f);
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
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, size);

            var background = new GameObject("Background");
            background.transform.SetParent(toggleObject.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(10f, 0f);
            backgroundRect.sizeDelta = new Vector2(18f, 18f);
            background.AddComponent<Image>().color = new Color(0.05f, 0.16f, 0.18f, 0.95f);

            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            var checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = new Vector2(-5f, -5f);
            checkmark.AddComponent<Image>().color = new Color(0f, 0.85f, 1f, 1f);

            var labelText = Text(name + "Label", toggleObject.transform, label, 13, TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(112f, 24f));
            labelText.raycastTarget = false;

            var toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = isOn;
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
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, size);

            var background = Panel("Background", sliderObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.05f, 0.17f, 0.2f, 0.95f));
            background.offsetMin = new Vector2(0f, 8f);
            background.offsetMax = new Vector2(0f, -8f);

            var fillArea = Panel("Fill Area", sliderObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear);
            fillArea.offsetMin = new Vector2(4f, 8f);
            fillArea.offsetMax = new Vector2(-4f, -8f);

            var fill = Panel("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0.8f, 1f, 0.9f));

            var handleArea = Panel("Handle Slide Area", sliderObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.clear);
            var handle = Panel("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 24f), new Color(0.9f, 0.96f, 1f, 1f));

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
            ConfigureRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, size);
            var image = inputObject.AddComponent<Image>();
            image.color = new Color(0.02f, 0.12f, 0.19f, 0.96f);
            var outline = inputObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.10f, 0.50f, 0.70f, 0.28f);
            outline.effectDistance = new Vector2(1f, -1f);

            var text = Text(name + "Text", inputObject.transform, value, 14, TextAnchor.MiddleLeft, Color.white, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(-20f, -10f));
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;

            var placeholderText = Text(name + "Placeholder", inputObject.transform, placeholder, 14, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.45f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(-20f, -10f));
            placeholderText.raycastTarget = false;

            var inputField = inputObject.AddComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            inputField.caretWidth = 2;
            inputField.text = value;
            return inputField;
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

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
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
    }
}
