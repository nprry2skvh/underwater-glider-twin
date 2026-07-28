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

        private void ConfigureResponsiveBottomDrawer(RectTransform drawer)
        {
            if (drawer == null || bottomDrawerContent != null)
            {
                return;
            }

            drawer.anchorMin = new Vector2(0f, 0f);
            drawer.anchorMax = new Vector2(1f, 0f);
            drawer.pivot = new Vector2(0.5f, 0f);
            drawer.anchoredPosition = new Vector2(0f, UiFactory.PlaybackControlsBottomOffset);
            drawer.sizeDelta = new Vector2(0f, 48f);

            var header = UiFactory.Panel("MissionConfigurationDrawerHeader", drawer, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-16f, 44f), new Color(0.02f, 0.12f, 0.18f, 0.98f));
            UiFactory.Text("MissionConfigurationDrawerLabel", header, "任务参数", 16, TextAnchor.MiddleLeft, UiFactory.CommandText, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(180f, -8f));
            bottomDrawerToggleButton = UiFactory.Button("MissionConfigurationDrawerToggleButton", header, "展开参数", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(108f, 30f));
            bottomDrawerToggleButton.onClick.AddListener(ToggleBottomDrawer);

            bottomDrawerViewport = UiFactory.Panel("MissionConfigurationViewport", drawer, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(-20f, -72f), new Color(0f, 0f, 0f, 0.08f));
            bottomDrawerViewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            bottomDrawerContent = UiFactory.Panel("MissionConfigurationContent", bottomDrawerViewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f), Color.clear);
            var grid = bottomDrawerContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 8, 8);
            grid.spacing = new Vector2(8f, 8f);
            grid.cellSize = new Vector2(352f, 42f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperLeft;
            var fitter = bottomDrawerContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            bottomDrawerScrollRect = drawer.gameObject.AddComponent<ScrollRect>();
            bottomDrawerScrollRect.viewport = bottomDrawerViewport;
            bottomDrawerScrollRect.content = bottomDrawerContent;
            bottomDrawerScrollRect.horizontal = false;
            bottomDrawerScrollRect.vertical = true;
            bottomDrawerScrollRect.movementType = ScrollRect.MovementType.Clamped;

            // The legacy field builders keep their stable object names and handlers.  Reparenting
            // them into a grid removes fixed x coordinates while retaining their public UI contract.
            for (var index = drawer.childCount - 1; index >= 0; index--)
            {
                var child = drawer.GetChild(index) as RectTransform;
                if (child == null || child == header || child == bottomDrawerViewport)
                {
                    continue;
                }

                child.SetParent(bottomDrawerContent, false);
                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.pivot = new Vector2(0.5f, 0.5f);
                child.anchoredPosition = Vector2.zero;
                child.sizeDelta = grid.cellSize;
                var element = child.gameObject.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
                element.minWidth = grid.cellSize.x;
                element.preferredWidth = grid.cellSize.x;
                element.minHeight = grid.cellSize.y;
                element.preferredHeight = grid.cellSize.y;
            }

            SetBottomDrawerExpanded(false);
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
            drawer.sizeDelta = new Vector2(0f, expanded ? 378f : 48f);
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
            content.sizeDelta = new Vector2(760f, drawer.name == "FlightLegDrawerPanel" ? 350f : 620f);
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
}
