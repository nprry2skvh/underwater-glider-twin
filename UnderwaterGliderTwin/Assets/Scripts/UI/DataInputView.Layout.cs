using System;
using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.UI
{
    public sealed partial class DataInputView
    {
        private RectTransform bottomDrawerContent;
        private RectTransform bottomDrawerViewport;
        private RectTransform configurationSummaryBar;
        private RectTransform configurationExpandedContent;
        private ScrollRect bottomDrawerScrollRect;
        private Button bottomDrawerToggleButton;
        private bool bottomDrawerExpanded;
        private Canvas oceanCurrentModalCanvas;
        private RectTransform oceanCurrentModalOverlay;
        private RectTransform oceanCurrentDrawerParent;
        private bool bottomDrawerExpandedBeforeModal;
        private bool bottomDrawerExpandedBeforeFlightLeg;
        private const float ExpandedTaskDrawerHeight = 320f;
        private const float TaskParameterFieldWidth = 280f;
        private const float DrawerScrollSensitivity = 45f;

        public bool ConfigurationExpandedForTests => bottomDrawerExpanded;

        private void ConfigureResponsiveBottomDrawer(RectTransform drawer)
        {
            if (drawer == null)
            {
                return;
            }

            drawer.gameObject.SetActive(true);
            drawer.anchorMin = new Vector2(0f, 1f);
            drawer.anchorMax = new Vector2(1f, 1f);
            drawer.pivot = new Vector2(0.5f, 1f);
            drawer.anchoredPosition = new Vector2(0f, -UiFactory.CommandCenterHeaderHeight);
            drawer.sizeDelta = new Vector2(0f, 48f);

            configurationSummaryBar = EnsureDrawerPanel(drawer, "ConfigurationSummaryBar", new Color(0.02f, 0.12f, 0.18f, 0.98f));
            configurationSummaryBar.SetAsFirstSibling();
            ConfigureRect(configurationSummaryBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-16f, 44f));
            var legacyHeader = EnsureDrawerPanel(configurationSummaryBar, "MissionConfigurationDrawerHeader", Color.clear);
            ConfigureRect(legacyHeader, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            legacyHeader.GetComponent<Image>().raycastTarget = false;
            EnsureDrawerText("MissionConfigurationDrawerLabel", configurationSummaryBar, "任务参数", 16, TextAnchor.MiddleLeft, UiFactory.CommandText, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(180f, -8f));
            bottomDrawerToggleButton = EnsureDrawerButton("MissionConfigurationDrawerToggleButton", configurationSummaryBar, "展开参数", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(108f, 30f));
            bottomDrawerToggleButton.onClick.RemoveListener(ToggleBottomDrawer);
            bottomDrawerToggleButton.onClick.AddListener(ToggleBottomDrawer);

            configurationExpandedContent = EnsureDrawerPanel(drawer, "ConfigurationExpandedContent", Color.clear);
            configurationExpandedContent.SetSiblingIndex(Mathf.Min(1, drawer.childCount - 1));
            ConfigureRect(configurationExpandedContent, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            EnsureCanvasGroup(configurationExpandedContent.gameObject);
            EnsureLayoutElement(configurationExpandedContent.gameObject);

            bottomDrawerViewport = EnsureDrawerPanel(configurationExpandedContent, "ConfigurationScrollViewport", new Color(0f, 0f, 0f, 0.08f));
            ConfigureRect(bottomDrawerViewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bottomDrawerViewport.offsetMin = new Vector2(10f, 10f);
            bottomDrawerViewport.offsetMax = new Vector2(-10f, -10f);
            var legacyViewport = EnsureDrawerPanel(configurationExpandedContent, "MissionConfigurationViewport", Color.clear);
            ConfigureRect(legacyViewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            legacyViewport.offsetMin = new Vector2(10f, 10f);
            legacyViewport.offsetMax = new Vector2(-10f, -44f);
            legacyViewport.SetAsFirstSibling();
            var legacyViewportMask = legacyViewport.gameObject.GetComponent<Mask>();
            if (legacyViewportMask == null)
            {
                legacyViewportMask = legacyViewport.gameObject.AddComponent<Mask>();
            }
            legacyViewportMask.showMaskGraphic = false;
            legacyViewport.GetComponent<Image>().raycastTarget = false;
            var viewportMask = bottomDrawerViewport.gameObject.GetComponent<Mask>();
            if (viewportMask == null)
            {
                viewportMask = bottomDrawerViewport.gameObject.AddComponent<Mask>();
            }
            viewportMask.showMaskGraphic = false;
            bottomDrawerViewport.GetComponent<Image>().raycastTarget = false;
            bottomDrawerContent = EnsureDrawerPanel(bottomDrawerViewport, "MissionConfigurationContent", Color.clear);
            ConfigureRect(bottomDrawerContent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
            var contentLayout = bottomDrawerContent.gameObject.GetComponent<VerticalLayoutGroup>();
            if (contentLayout == null)
            {
                contentLayout = bottomDrawerContent.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            contentLayout.padding = new RectOffset(8, 8, 6, 8);
            contentLayout.spacing = 6f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var fitter = bottomDrawerContent.gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = bottomDrawerContent.gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bottomDrawerScrollRect = configurationExpandedContent.gameObject.GetComponent<ScrollRect>();
            if (bottomDrawerScrollRect == null)
            {
                bottomDrawerScrollRect = configurationExpandedContent.gameObject.AddComponent<ScrollRect>();
            }
            bottomDrawerScrollRect.viewport = bottomDrawerViewport;
            bottomDrawerScrollRect.content = bottomDrawerContent;
            bottomDrawerScrollRect.horizontal = false;
            bottomDrawerScrollRect.vertical = true;
            bottomDrawerScrollRect.movementType = ScrollRect.MovementType.Clamped;
            ConfigureFastDrawerScroll(bottomDrawerScrollRect);
            DisableRootDrawerScroll(drawer);

            var missionSection = bottomDrawerContent.Find("MissionSectionCard") as RectTransform
                ?? CreateSectionCard("MissionSectionCard", "任务与预测", bottomDrawerContent);
            var simulationSection = bottomDrawerContent.Find("SimulationSectionCard") as RectTransform
                ?? CreateSectionCard("SimulationSectionCard", "仿真参数", bottomDrawerContent);
            var oceanSection = bottomDrawerContent.Find("OceanSectionCard") as RectTransform
                ?? CreateSectionCard("OceanSectionCard", "海流与航段", bottomDrawerContent);

            EnsureCompositeField(missionSection, "CsvSourceLabel", "CsvPathInput", "LoadCsvButton", "CsvPathField", true);
            EnsureCompositeField(missionSection, "PredictionModelLabel", FindExistingModelButtonName(), null, "PredictionModelField", false);
            EnsureCompositeField(missionSection, "PredictionHorizonLabel", "PredictionHorizonInput", "ApplyPredictionConfigButton", "PredictionHorizonField", false);
            EnsureCompositeField(missionSection, null, "PredictionToggleButton", "PredictionRuntimeLabel", "PredictionRuntimeField", false);

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
            HideLegacyTextChild("TitleText");
            HideLegacyTextChild("ModelLabel");
            HideLegacyTextChild("SimulationLabel");
            HideLegacyTextChild("OceanCurrentLabel");
            MoveToSectionFooter(missionSection, "MissionConfigurationStatus");
            HideLegacyConfigurationGroups(drawer);

            SetConfigurationExpanded(false);
        }

        private static RectTransform EnsureDrawerPanel(Transform parent, string name, Color color)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing == null)
            {
                existing = UiFactory.Panel(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, color);
            }

            var image = existing.GetComponent<Image>();
            if (image == null)
            {
                image = existing.gameObject.AddComponent<Image>();
            }
            image.color = color;
            return existing;
        }

        private static Text EnsureDrawerText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var existing = parent.Find(name);
            var label = existing != null ? existing.GetComponent<Text>() : null;
            if (label == null)
            {
                label = UiFactory.Text(name, parent, text, fontSize, alignment, color, anchorMin, anchorMax, pivot, anchoredPosition, size);
            }

            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            ConfigureRect(label.transform as RectTransform, anchorMin, anchorMax, pivot, anchoredPosition, size);
            return label;
        }

        private static Button EnsureDrawerButton(
            string name,
            Transform parent,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var existing = parent.Find(name);
            var button = existing != null ? existing.GetComponent<Button>() : null;
            if (button == null)
            {
                button = UiFactory.Button(name, parent, text, anchorMin, anchorMax, pivot, anchoredPosition, size);
            }

            UiFactory.SetButtonText(button, text);
            ConfigureRect(button.transform as RectTransform, anchorMin, anchorMax, pivot, anchoredPosition, size);
            return button;
        }

        private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject gameObject)
        {
            var group = gameObject.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private static LayoutElement EnsureLayoutElement(GameObject gameObject)
        {
            var element = gameObject.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = gameObject.AddComponent<LayoutElement>();
            }

            return element;
        }

        private static void DisableRootDrawerScroll(RectTransform drawer)
        {
            if (drawer == null)
            {
                return;
            }

            var rootScroll = drawer.GetComponent<ScrollRect>();
            if (rootScroll == null)
            {
                return;
            }

            rootScroll.StopMovement();
            rootScroll.viewport = null;
            rootScroll.content = null;
            rootScroll.horizontal = false;
            rootScroll.vertical = false;
            rootScroll.enabled = false;
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

        private void EnsureCompositeField(Transform section, string labelName, string controlName, string secondaryName, string wrapperName, bool fullWidth)
        {
            if (section == null || section.Find(wrapperName) != null)
            {
                return;
            }

            CreateCompositeField(section, labelName, controlName, secondaryName, wrapperName, fullWidth);
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
            cardLayout.childForceExpandWidth = false;
            cardLayout.childForceExpandHeight = false;
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = fullWidth ? 62f : 56f;
            element.minWidth = fullWidth ? 560f : TaskParameterFieldWidth;
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
            ConfigureCompositeWidths(card, fullWidth);
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

            if (RequiresLegacyFieldWrapper(child, childName))
            {
                var wrapper = EnsureLegacyFieldWrapper(fields, childName, GetLegacyFieldLabel(childName));
                SafeSetParent(child, wrapper);
                NormalizeLayoutChild(child, 28f);
                return;
            }

            SafeSetParent(child, fields);
            NormalizeLayoutChild(child);
            NormalizeFieldCard(child);
            SetLegacyButtonLabel(child, childName);
        }

        private static bool RequiresLegacyFieldWrapper(RectTransform child, string childName)
        {
            return child != null
                && (child.GetComponent<InputField>() != null
                    || (childName == "ReferenceCycleDurationReadout" && child.GetComponent<Text>() != null));
        }

        private static RectTransform EnsureLegacyFieldWrapper(Transform fields, string childName, string labelText)
        {
            var wrapperName = childName + "Field";
            var wrapper = fields.Find(wrapperName) as RectTransform;
            if (wrapper != null && wrapper.GetComponent<InputField>() != null)
            {
                wrapper = null;
            }
            if (wrapper == null)
            {
                wrapper = UiFactory.Panel(wrapperName, fields, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.04f, 0.18f, 0.24f, 0.8f));
            }

            var layout = wrapper.GetComponent<VerticalLayoutGroup>() ?? wrapper.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 5);
            layout.spacing = 3f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = wrapper.GetComponent<LayoutElement>() ?? wrapper.gameObject.AddComponent<LayoutElement>();
            element.minWidth = TaskParameterFieldWidth;
            element.preferredHeight = 62f;

            var labelName = wrapperName + "Label";
            var label = wrapper.Find(labelName)?.GetComponent<Text>();
            if (label == null)
            {
                label = UiFactory.Text(labelName, wrapper, labelText, 12, TextAnchor.MiddleLeft, UiFactory.CommandText,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var labelElement = label.gameObject.AddComponent<LayoutElement>();
                labelElement.preferredHeight = 18f;
                labelElement.minHeight = 18f;
                labelElement.flexibleWidth = 1f;
            }

            return wrapper;
        }

        private static string GetLegacyFieldLabel(string childName)
        {
            switch (childName)
            {
                case "SimulationCyclesInputField": return "循环次数";
                case "SimulationDurationInputField": return "单航段安全上限 (s)";
                case "SimulationDepthInputField": return "深度 (m)";
                case "SimulationWaterColumnInputField": return "水柱 (m)";
                case "SimulationHeadingInputField": return "航向 (°)";
                case "SimulationHeadingDeltaInputField": return "转向 (°)";
                case "SimulationPitchInputField": return "默认俯仰 (°)";
                case "SimulationRollInputField": return "默认横滚 (°)";
                case "ReferenceCycleDurationReadout": return "航段安全参考";
                case "OceanCurrentMinDepthInputField": return "最小 (m)";
                case "OceanCurrentMaxDepthInputField": return "最大 (m)";
                case "OceanCurrentEastwardInputField": return "东流 (m/s)";
                case "OceanCurrentNorthwardInputField": return "北流 (m/s)";
                case "MissionLongitudeInputField": return "经度 (°)";
                case "MissionLatitudeInputField": return "纬度 (°)";
                default: return childName;
            }
        }

        private static void SetLegacyButtonLabel(RectTransform child, string childName)
        {
            if (childName != "ApplyReferenceCycleButton")
            {
                return;
            }

            var button = child.GetComponent<Button>();
            if (button != null)
            {
                UiFactory.SetButtonText(button, "采用参考");
            }
        }

        private void MoveToSectionFooter(Transform section, string childName)
        {
            var child = FindDirectChild(childName);
            if (child == null)
            {
                return;
            }

            // Keep the asynchronous status line in the mission card's own layout so
            // it follows the mission fields instead of overlapping them at a fixed
            // position in the expanded drawer.
            SafeSetParent(child, section);
            child.SetAsLastSibling();
            NormalizeLayoutChild(child, 44f);
            var layoutElement = child.gameObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = false;
            var text = child.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleLeft;
            }
        }

        private void HideLegacyTextChild(string childName)
        {
            var child = FindDirectChild(childName);
            if (child == null && configurationExpandedContent != null)
            {
                child = configurationExpandedContent.Find(childName) as RectTransform;
            }
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
                text.canvasRenderer.SetAlpha(0f);
            }

            if (configurationExpandedContent == null || !child.IsChildOf(configurationExpandedContent))
            {
                SafeSetParent(child, bottomDrawerContent);
            }
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

            SafeSetParent(child, parent);
            NormalizeLayoutChild(child, preferredHeight);
        }

        private static bool SafeSetParent(RectTransform child, Transform parent)
        {
            if (child == null || parent == null || child.parent == parent)
            {
                return child != null && parent != null;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(child))
            {
                return false;
            }
#endif
            child.SetParent(parent, false);
            return true;
        }

        private RectTransform FindDirectChild(string childName)
        {
            if (string.IsNullOrWhiteSpace(childName) || configurationPanel == null)
            {
                return null;
            }

            var directChild = configurationPanel.Find(childName) as RectTransform;
            if (directChild != null)
            {
                return directChild;
            }

            foreach (var alias in GetPrefabChildAliases(childName))
            {
                directChild = configurationPanel.Find(alias) as RectTransform;
                if (directChild != null)
                {
                    return directChild;
                }
            }

            return null;
        }

        private static string[] GetPrefabChildAliases(string childName)
        {
            switch (childName)
            {
                case "PredictionModelLabel": return new[] { "ModelLabel" };
                case "SimulationCyclesInputField": return new[] { "SimulationCyclesInput" };
                case "SimulationDurationInputField": return new[] { "SimulationDurationInput" };
                case "SimulationDepthInputField": return new[] { "SimulationDepthInput", "TargetDepthInput" };
                case "SimulationWaterColumnInputField": return new[] { "SimulationWaterColumnInput", "WaterColumnDepthInput" };
                case "SimulationHeadingInputField": return new[] { "SimulationHeadingInput", "HeadingInput" };
                case "SimulationHeadingDeltaInputField": return new[] { "SimulationHeadingDeltaInput", "HeadingDeltaInput" };
                case "SimulationPitchInputField": return new[] { "SimulationPitchInput", "PitchInput" };
                case "SimulationRollInputField": return new[] { "SimulationRollInput", "RollInput" };
                case "ReferenceCycleDurationReadout": return new[] { "ReferenceCycleDurationValue" };
                case "OceanCurrentMinDepthInputField": return new[] { "MinDepthInput" };
                case "OceanCurrentMaxDepthInputField": return new[] { "MaxDepthInput" };
                case "OceanCurrentEastwardInputField": return new[] { "EastwardInput" };
                case "OceanCurrentNorthwardInputField": return new[] { "NorthwardInput" };
                case "MissionLongitudeInputField": return new[] { "MissionLongitudeInput" };
                case "MissionLatitudeInputField": return new[] { "MissionLatitudeInput" };
                case "OceanCurrentPreviousLayerButton": return new[] { "PreviousLayerButton" };
                case "OceanCurrentNextLayerButton": return new[] { "NextLayerButton" };
                case "OceanCurrentAddLayerButton": return new[] { "AddLayerButton" };
                case "OceanCurrentSaveLayerButton": return new[] { "SaveLayerButton" };
                case "OceanCurrentDeleteLayerButton": return new[] { "DeleteLayerButton", "RemoveButton" };
                case "OceanCurrentLookupButton": return new[] { "LookupButton" };
                case "OceanCurrentLayerSummary": return new[] { "LayerSummaryText" };
                case "OceanCurrentDrawerButton": return new[] { "DrawerButton" };
                default: return Array.Empty<string>();
            }
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
            layout.childForceExpandWidth = false;
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
                var element = childRect.GetComponent<LayoutElement>();
                if (input != null)
                {
                    element.minWidth = 0f;
                    element.preferredWidth = 0f;
                    element.flexibleWidth = 1f;
                }
                else
                {
                    element.minWidth = 0f;
                    element.preferredWidth = 0f;
                    element.flexibleWidth = 1f;
                }
            }
        }

        private static void ConfigureCompositeWidths(RectTransform card, bool fullWidth)
        {
            foreach (Transform child in card)
            {
                var childRect = child as RectTransform;
                if (childRect == null)
                {
                    continue;
                }

                var element = childRect.GetComponent<LayoutElement>() ?? childRect.gameObject.AddComponent<LayoutElement>();
                var input = childRect.GetComponent<InputField>();
                var button = childRect.GetComponent<Button>();
                if (input != null && fullWidth)
                {
                    element.minWidth = 180f;
                    element.preferredWidth = 0f;
                    element.flexibleWidth = 1f;
                }
                else if (input != null)
                {
                    element.minWidth = 0f;
                    element.preferredWidth = 0f;
                    element.flexibleWidth = 1f;
                }
                else if (button != null)
                {
                    element.minWidth = 112f;
                    element.preferredWidth = 112f;
                    element.flexibleWidth = 0f;
                }
                else
                {
                    element.minWidth = 0f;
                    element.preferredWidth = 0f;
                    element.flexibleWidth = 1f;
                }
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
            if (main == null || mainCanvas == null)
            {
                return null;
            }

            var modalRoot = mainCanvas.Find("ModalRoot") as RectTransform;
            if (modalRoot == null)
            {
                modalRoot = new GameObject("ModalRoot", typeof(RectTransform)).GetComponent<RectTransform>();
                modalRoot.SetParent(mainCanvas, false);
                modalRoot.anchorMin = Vector2.zero;
                modalRoot.anchorMax = Vector2.one;
                modalRoot.offsetMin = Vector2.zero;
                modalRoot.offsetMax = Vector2.zero;
            }

            oceanCurrentModalOverlay = modalRoot.Find("OceanCurrentModalCanvas") as RectTransform;
            if (oceanCurrentModalOverlay == null)
            {
                var overlayObject = new GameObject("OceanCurrentModalCanvas", typeof(RectTransform));
                overlayObject.transform.SetParent(modalRoot, false);
                oceanCurrentModalOverlay = overlayObject.GetComponent<RectTransform>();
            }

            oceanCurrentModalOverlay.anchorMin = Vector2.zero;
            oceanCurrentModalOverlay.anchorMax = Vector2.one;
            oceanCurrentModalOverlay.offsetMin = Vector2.zero;
            oceanCurrentModalOverlay.offsetMax = Vector2.zero;

            var blockerRect = oceanCurrentModalOverlay.Find("OceanCurrentModalRaycastBlocker") as RectTransform;
            if (blockerRect == null)
            {
                var blockerObject = new GameObject("OceanCurrentModalRaycastBlocker", typeof(RectTransform), typeof(Image));
                blockerObject.transform.SetParent(oceanCurrentModalOverlay, false);
                blockerRect = blockerObject.GetComponent<RectTransform>();
            }

            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            var blocker = blockerRect.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.18f);
            blocker.raycastTarget = true;
            oceanCurrentDrawerParent = oceanCurrentModalOverlay;
            oceanCurrentModalOverlay.gameObject.SetActive(false);
            oceanCurrentModalCanvas = main;
            return oceanCurrentModalCanvas;
        }

        private void ToggleBottomDrawer()
        {
            SetBottomDrawerExpanded(!bottomDrawerExpanded);
        }

        public void ToggleAdvancedConfiguration()
        {
            ToggleBottomDrawer();
        }

        public void SetConfigurationExpanded(bool expanded)
        {
            bottomDrawerExpanded = expanded;
            if (configurationExpandedContent == null)
            {
                return;
            }

            var expandedHeight = Mathf.Max(0f, ExpandedTaskDrawerHeight - 48f);
            var group = EnsureCanvasGroup(configurationExpandedContent.gameObject);
            group.alpha = expanded ? 1f : 0f;
            group.interactable = expanded;
            group.blocksRaycasts = expanded;

            var contentElement = EnsureLayoutElement(configurationExpandedContent.gameObject);
            contentElement.minHeight = expanded ? expandedHeight : 0f;
            contentElement.preferredHeight = expanded ? expandedHeight : 0f;
            contentElement.flexibleHeight = 0f;
            configurationExpandedContent.sizeDelta = new Vector2(0f, expanded ? expandedHeight : 0f);

            var drawer = configurationPanel;
            if (drawer != null)
            {
                drawer.sizeDelta = new Vector2(0f, expanded ? ExpandedTaskDrawerHeight : 48f);
                var drawerElement = EnsureLayoutElement(drawer.gameObject);
                drawerElement.minHeight = expanded ? ExpandedTaskDrawerHeight : 48f;
                drawerElement.preferredHeight = expanded ? ExpandedTaskDrawerHeight : 48f;
            }

            if (bottomDrawerToggleButton != null)
            {
                UiFactory.SetButtonText(bottomDrawerToggleButton, expanded ? "收起参数" : "展开参数");
                bottomDrawerToggleButton.transform.SetAsLastSibling();
            }

            if (configurationSummaryBar != null)
            {
                configurationSummaryBar.SetAsLastSibling();
            }
        }

        private void SetBottomDrawerExpanded(bool expanded)
        {
            SetConfigurationExpanded(expanded);
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

        private static void ConfigureOceanCurrentModalDrawer(RectTransform drawer)
        {
            if (drawer == null)
            {
                return;
            }

            var canvas = drawer.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            var canvasSize = canvasRect != null && canvasRect.rect.width > 0f && canvasRect.rect.height > 0f
                ? canvasRect.rect.size
                : new Vector2(1920f, 1080f);
            var modalWidth = Mathf.Min(760f, Mathf.Max(420f, canvasSize.x - 48f));
            var modalHeight = Mathf.Min(760f, Mathf.Max(420f, canvasSize.y - 96f));
            drawer.anchorMin = new Vector2(0.5f, 0.5f);
            drawer.anchorMax = new Vector2(0.5f, 0.5f);
            drawer.pivot = new Vector2(0.5f, 0.5f);
            drawer.anchoredPosition = Vector2.zero;
            drawer.sizeDelta = new Vector2(modalWidth, modalHeight);

            if (drawer.Find("EditorViewport") != null)
            {
                return;
            }

            var fixedNames = new System.Collections.Generic.HashSet<string>
            {
                "OceanCurrentDrawerTitle",
                "OceanCurrentDrawerCloseButton",
                "OceanCurrentDrawerStatus"
            };
            var existingChildren = new System.Collections.Generic.List<Transform>();
            for (var index = 0; index < drawer.childCount; index++)
            {
                var child = drawer.GetChild(index);
                if (!fixedNames.Contains(child.name))
                {
                    existingChildren.Add(child);
                }
            }

            var viewportObject = new GameObject("EditorViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(drawer, false);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12f, 84f);
            viewport.offsetMax = new Vector2(-12f, -56f);
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("EditorContent", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            var content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            var contentLayout = contentObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
            contentLayout.spacing = 6f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = contentObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentLayout.enabled = false;
            contentFitter.enabled = false;
            var cursorY = 0f;
            foreach (var child in existingChildren)
            {
                child.SetParent(content, false);
                cursorY += PlaceModalContentChild(child as RectTransform, modalWidth - 24f, cursorY);
            }

            content.sizeDelta = new Vector2(0f, Mathf.Max(420f, cursorY + 8f));

            viewport.SetAsFirstSibling();
            var scroll = drawer.gameObject.GetComponent<ScrollRect>() ?? drawer.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            ConfigureFastDrawerScroll(scroll);
            Canvas.ForceUpdateCanvases();
        }

        private static float PlaceModalContentChild(RectTransform child, float availableWidth, float topOffset)
        {
            if (child == null)
            {
                return 0f;
            }

            var element = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
            element.minWidth = availableWidth;
            element.preferredWidth = availableWidth;
            element.flexibleWidth = 1f;

            var preferredHeight = Mathf.Max(28f, child.rect.height);
            if (preferredHeight <= 0f)
            {
                preferredHeight = 32f;
            }
            element.minHeight = preferredHeight;
            element.preferredHeight = preferredHeight;
            element.flexibleHeight = 0f;
            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(1f, 1f);
            child.pivot = new Vector2(0.5f, 1f);
            child.anchoredPosition = new Vector2(0f, -topOffset);
            child.sizeDelta = new Vector2(0f, preferredHeight);
            return preferredHeight + 6f;
        }

        private static void ConfigureInlineDrawer(RectTransform drawer)
        {
            if (drawer == null)
            {
                return;
            }

            // Flight-leg editing lives above the fixed playback bar.  Keeping this
            // offset here makes it impossible for the editor and operations bar to
            // claim the same pixels at any resolution.
            drawer.anchorMin = new Vector2(0f, 0f);
            drawer.anchorMax = new Vector2(1f, 0f);
            drawer.pivot = new Vector2(0.5f, 0f);
            drawer.anchoredPosition = new Vector2(0f, UiFactory.CommandCenterOperationsTopOffset);
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
            ConfigureFastDrawerScroll(scroll);
        }

        private void ConfigureBoundParameterDrawerScrolling()
        {
            ConfigureFastDrawerScroll(configurationPanel);
            ConfigureFastDrawerScroll(oceanCurrentDrawer);
            ConfigureFastDrawerScroll(flightLegDrawer);
        }

        private static void ConfigureFastDrawerScroll(RectTransform drawer)
        {
            if (drawer == null)
            {
                return;
            }

            var rootScroll = drawer.GetComponent<ScrollRect>();
            ConfigureFastDrawerScroll(rootScroll);
            foreach (var childScroll in drawer.GetComponentsInChildren<ScrollRect>(true))
            {
                ConfigureFastDrawerScroll(childScroll);
            }
        }

        private static void ConfigureFastDrawerScroll(ScrollRect scroll)
        {
            if (scroll == null)
            {
                return;
            }

            scroll.scrollSensitivity = DrawerScrollSensitivity;
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
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(canvasRect.rect.height, 1080f) * 0.35f);
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
        private const float FixedCardWidth = 280f;
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
            var columns = available > FixedCardWidth * 4f + HorizontalGap * 3f
                ? 4
                : available > FixedCardWidth * 3f + HorizontalGap * 2f
                    ? 3
                    : available >= FixedCardWidth * 2f + HorizontalGap
                        ? 2
                        : 1;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.spacing = new Vector2(HorizontalGap, VerticalGap);
            grid.cellSize = new Vector2(FixedCardWidth, 62f);
            if (fitter != null)
            {
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }
    }
}
