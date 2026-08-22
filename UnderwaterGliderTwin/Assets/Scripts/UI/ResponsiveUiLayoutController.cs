using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public enum RuntimeUiSideDrawer
    {
        Telemetry,
        Status
    }

    public sealed class ResponsiveUiLayoutController : MonoBehaviour
    {
        [SerializeField] private float drawerAnimationDuration = 0.16f;
        [SerializeField] private bool animationsEnabled = true;

        private RuntimeUiRoot runtimeRoot;
        private RuntimeUiReferences references;
        private RectTransform mainBody;
        private RectTransform telemetryColumn;
        private RectTransform viewportColumn;
        private RectTransform statusColumn;
        private Button telemetryToggle;
        private Button statusToggle;
        private Image drawerScrim;
        private CanvasGroup telemetryGroup;
        private CanvasGroup statusGroup;
        private CanvasGroup scrimGroup;
        private Coroutine transition;
        private GameObject focusReturnTarget;
        private RuntimeUiSideDrawer? openDrawer;
        private RuntimeUiLayoutMode currentMode = RuntimeUiLayoutMode.FullThreeColumn;
        private bool hasResolvedMode;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private bool suppressAutomaticRefreshForTests;

        public RuntimeUiLayoutMode CurrentMode => currentMode;

        public void Bind(RuntimeUiRoot root, RuntimeUiReferences boundReferences)
        {
            runtimeRoot = root;
            references = boundReferences ?? root?.References;
            CacheReferences();
            WireDrawerButtons();
            CloseSideDrawerImmediate();
        }

        public void SetAnimationsEnabledForTests(bool enabled)
        {
            animationsEnabled = enabled;
            suppressAutomaticRefreshForTests = true;
        }

        public void RefreshForScreen(float width, float height)
        {
            var previous = hasResolvedMode ? currentMode : RuntimeUiLayoutMode.FullThreeColumn;
            var next = ResponsiveUiLayoutPolicy.Resolve(width, height, previous);
            currentMode = next;
            hasResolvedMode = true;
            lastScreenWidth = Mathf.RoundToInt(width);
            lastScreenHeight = Mathf.RoundToInt(height);

            if (next != RuntimeUiLayoutMode.Drawer)
            {
                openDrawer = null;
                StopTransition();
                ApplyThreeColumnState();
                ApplyTypography(width, height);
                return;
            }

            ApplyDrawerModeState();
            ApplyTypography(width, height);
        }

        public void OpenSideDrawer(RuntimeUiSideDrawer drawer)
        {
            if (currentMode != RuntimeUiLayoutMode.Drawer)
            {
                return;
            }

            focusReturnTarget = drawer == RuntimeUiSideDrawer.Telemetry
                ? telemetryToggle?.gameObject
                : statusToggle?.gameObject;
            openDrawer = drawer;
            StopTransition();
            ApplyColumnVisibility(drawer);
            SetCanvasState(GetColumnGroup(drawer), true, 1f);
            SetCanvasState(GetColumnGroup(Opposite(drawer)), false, 0f);
            SetScrimState(true, animationsEnabled ? 1f : 1f);

            if (animationsEnabled)
            {
                transition = StartCoroutine(AnimateDrawer(true, drawer));
            }
            else
            {
                SetCanvasState(GetColumnGroup(drawer), true, 1f);
                FocusDrawer(drawer);
            }
        }

        public void CloseSideDrawer()
        {
            if (!openDrawer.HasValue)
            {
                return;
            }

            StopTransition();
            var closing = openDrawer.Value;
            openDrawer = null;
            if (animationsEnabled)
            {
                transition = StartCoroutine(AnimateClose(closing));
            }
            else
            {
                CloseSideDrawerImmediate();
            }
        }

        public void ToggleAdvancedConfiguration()
        {
            var dataInput = FindObjectOfType<DataInputView>(true);
            dataInput?.ToggleAdvancedConfiguration();
        }

        private void Awake()
        {
            if (runtimeRoot == null)
            {
                runtimeRoot = GetComponentInParent<RuntimeUiRoot>();
            }

            if (references == null && runtimeRoot != null)
            {
                references = runtimeRoot.References;
            }

            CacheReferences();
            WireDrawerButtons();
        }

        private void Update()
        {
            if (!suppressAutomaticRefreshForTests && Screen.width > 0 && Screen.height > 0
                && (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
            {
                RefreshForScreen(Screen.width, Screen.height);
            }

            if (openDrawer.HasValue && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseSideDrawer();
            }
        }

        private void CacheReferences()
        {
            if (runtimeRoot != null && runtimeRoot.RuntimeCanvas != null)
            {
                var canvas = runtimeRoot.RuntimeCanvas.transform;
                mainBody = FindRect(canvas, "UiRoot/MainBody", mainBody);
                telemetryColumn = FindRect(canvas, "UiRoot/MainBody/TelemetryColumn", telemetryColumn);
                viewportColumn = FindRect(canvas, "UiRoot/MainBody/ViewportColumn", viewportColumn);
                statusColumn = FindRect(canvas, "UiRoot/MainBody/StatusColumn", statusColumn);
                telemetryToggle = FindButton(canvas, "UiRoot/DrawerEntryLayer/TelemetryDrawerToggle", telemetryToggle);
                statusToggle = FindButton(canvas, "UiRoot/DrawerEntryLayer/StatusDrawerToggle", statusToggle);
                drawerScrim = FindImage(canvas, "ModalRoot/DrawerScrim", drawerScrim);
            }

            if (references == null)
            {
                return;
            }

            var layout = references.layout;
            mainBody = layout.mainBody ?? mainBody;
            telemetryColumn = layout.telemetryColumn ?? telemetryColumn;
            viewportColumn = layout.viewportColumn ?? viewportColumn;
            statusColumn = layout.statusColumn ?? statusColumn;
            telemetryToggle = layout.telemetryDrawerToggle ?? telemetryToggle;
            statusToggle = layout.statusDrawerToggle ?? statusToggle;
            drawerScrim = layout.drawerScrim ?? drawerScrim;
            telemetryGroup = EnsureCanvasGroup(telemetryColumn);
            statusGroup = EnsureCanvasGroup(statusColumn);
            scrimGroup = drawerScrim != null ? EnsureCanvasGroup(drawerScrim.gameObject) : null;
        }

        private void ApplyTypography(float width, float height)
        {
            var canvas = runtimeRoot != null ? runtimeRoot.RuntimeCanvas : null;
            if (canvas == null)
            {
                return;
            }

            var profile = ResponsiveUiTypography.ForMode(currentMode, width, height);
            var logicalScale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            {
                var isButtonText = text.GetComponentInParent<Button>() != null;
                var isTitle = text.name.IndexOf("Title", System.StringComparison.OrdinalIgnoreCase) >= 0;
                var desiredSize = isTitle
                    ? profile.sectionTitleSize
                    : isButtonText
                        ? profile.buttonSize
                        : text.name.IndexOf("Label", System.StringComparison.OrdinalIgnoreCase) >= 0
                            ? profile.labelSize
                            : profile.valueSize;
                text.fontSize = Mathf.Max(text.fontSize, desiredSize);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.supportRichText = false;
            }

            var actualMinimum = ResponsiveUiTypography.GetActualPixelSize(profile.labelSize, logicalScale);
            if (actualMinimum < profile.minimumReadablePixelSize && currentMode == RuntimeUiLayoutMode.Drawer)
            {
                foreach (var text in canvas.GetComponentsInChildren<Text>(true))
                {
                    text.fontSize = Mathf.Max(text.fontSize, Mathf.CeilToInt(profile.minimumReadablePixelSize / logicalScale));
                }
            }
        }

        private void WireDrawerButtons()
        {
            if (telemetryToggle != null)
            {
                telemetryToggle.onClick.RemoveListener(OpenTelemetryDrawer);
                telemetryToggle.onClick.AddListener(OpenTelemetryDrawer);
            }

            if (statusToggle != null)
            {
                statusToggle.onClick.RemoveListener(OpenStatusDrawer);
                statusToggle.onClick.AddListener(OpenStatusDrawer);
            }
        }

        private void OpenTelemetryDrawer() => OpenSideDrawer(RuntimeUiSideDrawer.Telemetry);
        private void OpenStatusDrawer() => OpenSideDrawer(RuntimeUiSideDrawer.Status);

        private void ApplyThreeColumnState()
        {
            if (mainBody != null)
            {
                mainBody.gameObject.SetActive(true);
            }

            SetActive(telemetryColumn, true);
            SetActive(viewportColumn, true);
            SetActive(statusColumn, true);
            SetCanvasState(telemetryGroup, true, 1f);
            SetCanvasState(statusGroup, true, 1f);
            SetActive(telemetryToggle, false);
            SetActive(statusToggle, false);
            SetScrimState(false, 0f);
        }

        private void ApplyDrawerModeState()
        {
            if (mainBody != null)
            {
                mainBody.gameObject.SetActive(true);
            }

            SetActive(telemetryToggle, true);
            SetActive(statusToggle, true);
            SetActive(viewportColumn, true);
            SetCanvasState(telemetryGroup, openDrawer == RuntimeUiSideDrawer.Telemetry, openDrawer == RuntimeUiSideDrawer.Telemetry ? 1f : 0f);
            SetCanvasState(statusGroup, openDrawer == RuntimeUiSideDrawer.Status, openDrawer == RuntimeUiSideDrawer.Status ? 1f : 0f);
            if (!openDrawer.HasValue)
            {
                SetScrimState(false, 0f);
            }
        }

        private void ApplyColumnVisibility(RuntimeUiSideDrawer drawer)
        {
            SetActive(telemetryColumn, drawer == RuntimeUiSideDrawer.Telemetry);
            SetActive(viewportColumn, true);
            SetActive(statusColumn, drawer == RuntimeUiSideDrawer.Status);
        }

        private IEnumerator AnimateDrawer(bool opening, RuntimeUiSideDrawer drawer)
        {
            var group = GetColumnGroup(drawer);
            var start = opening ? 0f : group != null ? group.alpha : 1f;
            var end = opening ? 1f : 0f;
            var elapsed = 0f;
            while (elapsed < drawerAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = drawerAnimationDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / drawerAnimationDuration);
                SetCanvasState(group, true, Mathf.Lerp(start, end, t));
                SetScrimState(true, Mathf.Lerp(opening ? 0f : 1f, opening ? 1f : 0f, t));
                yield return null;
            }

            SetCanvasState(group, opening, end);
            if (!opening)
            {
                SetScrimState(false, 0f);
                RestoreFocus();
            }
            else
            {
                FocusDrawer(drawer);
            }

            transition = null;
        }

        private IEnumerator AnimateClose(RuntimeUiSideDrawer drawer)
        {
            yield return AnimateDrawer(false, drawer);
        }

        private void CloseSideDrawerImmediate()
        {
            openDrawer = null;
            StopTransition();
            SetCanvasState(telemetryGroup, false, 0f);
            SetCanvasState(statusGroup, false, 0f);
            SetActive(viewportColumn, true);
            SetScrimState(false, 0f);
            RestoreFocus();
        }

        private void FocusDrawer(RuntimeUiSideDrawer drawer)
        {
            var selectable = GetColumnGroup(drawer)?.GetComponentInChildren<Selectable>(true);
            if (selectable != null && EventSystem.current != null)
            {
                var current = EventSystem.current.currentSelectedGameObject;
                if (current != null && !current.transform.IsChildOf(transform))
                {
                    return;
                }

                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        private void RestoreFocus()
        {
            if (focusReturnTarget != null && EventSystem.current != null)
            {
                var current = EventSystem.current.currentSelectedGameObject;
                if (current != null && !current.transform.IsChildOf(transform))
                {
                    return;
                }

                EventSystem.current.SetSelectedGameObject(focusReturnTarget);
            }
        }

        private CanvasGroup GetColumnGroup(RuntimeUiSideDrawer drawer)
        {
            return drawer == RuntimeUiSideDrawer.Telemetry ? telemetryGroup : statusGroup;
        }

        private static RuntimeUiSideDrawer Opposite(RuntimeUiSideDrawer drawer)
        {
            return drawer == RuntimeUiSideDrawer.Telemetry ? RuntimeUiSideDrawer.Status : RuntimeUiSideDrawer.Telemetry;
        }

        private void SetScrimState(bool visible, float alpha)
        {
            if (drawerScrim != null)
            {
                drawerScrim.gameObject.SetActive(visible);
                drawerScrim.raycastTarget = visible;
            }

            if (scrimGroup != null)
            {
                scrimGroup.alpha = alpha;
                scrimGroup.interactable = visible;
                scrimGroup.blocksRaycasts = visible;
            }
        }

        private static void SetCanvasState(CanvasGroup group, bool visible, float alpha)
        {
            if (group == null)
            {
                return;
            }

            group.gameObject.SetActive(visible);
            group.alpha = alpha;
            group.interactable = visible && alpha > 0.99f;
            group.blocksRaycasts = visible && alpha > 0.01f;
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform rect)
        {
            return rect == null ? null : EnsureCanvasGroup(rect.gameObject);
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            var existing = gameObject.GetComponent<CanvasGroup>();
            return existing != null ? existing : gameObject.AddComponent<CanvasGroup>();
        }

        private static void SetActive(RectTransform rect, bool active)
        {
            if (rect != null)
            {
                rect.gameObject.SetActive(active);
            }
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null)
            {
                button.gameObject.SetActive(active);
            }
        }

        private void StopTransition()
        {
            if (transition != null)
            {
                StopCoroutine(transition);
                transition = null;
            }
        }

        private static RectTransform FindRect(Transform root, string path, RectTransform fallback)
        {
            var found = root != null ? root.Find(path) as RectTransform : null;
            return found ?? fallback;
        }

        private static Button FindButton(Transform root, string path, Button fallback)
        {
            var found = root != null ? root.Find(path)?.GetComponent<Button>() : null;
            return found ?? fallback;
        }

        private static Image FindImage(Transform root, string path, Image fallback)
        {
            var found = root != null ? root.Find(path)?.GetComponent<Image>() : null;
            return found ?? fallback;
        }
    }
}
