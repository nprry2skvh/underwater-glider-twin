using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ResponsiveUiLayoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator RefreshForScreen_UsesBoundariesWithoutOscillation_AndDrawersAreMutuallyExclusive()
        {
            using (var scope = new ResponsiveLayoutTestScope())
            {
                scope.Controller.SetAnimationsEnabledForTests(false);

                scope.Controller.RefreshForScreen(1366f, 768f);
                yield return null;

                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
                Assert.That(scope.TelemetryToggle.gameObject.activeSelf, Is.False);
                Assert.That(scope.StatusToggle.gameObject.activeSelf, Is.False);

                scope.Controller.RefreshForScreen(1280f, 720f);
                yield return null;
                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));

                scope.Controller.RefreshForScreen(1279f, 720f);
                yield return null;
                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.Drawer));
                Assert.That(scope.TelemetryToggle.gameObject.activeSelf, Is.True);
                Assert.That(scope.StatusToggle.gameObject.activeSelf, Is.True);

                scope.Controller.RefreshForScreen(1279f, 720f);
                yield return null;
                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.Drawer));

                scope.Controller.OpenSideDrawer(RuntimeUiSideDrawer.Telemetry);
                yield return null;
                Assert.That(scope.TelemetryColumn.gameObject.activeSelf, Is.True);
                Assert.That(scope.StatusColumn.gameObject.activeSelf, Is.False);

                scope.Controller.OpenSideDrawer(RuntimeUiSideDrawer.Status);
                yield return null;
                Assert.That(scope.TelemetryColumn.gameObject.activeSelf, Is.False);
                Assert.That(scope.StatusColumn.gameObject.activeSelf, Is.True);

                scope.Controller.RefreshForScreen(1296f, 720f);
                yield return null;

                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
                Assert.That(scope.TelemetryColumn.parent, Is.EqualTo(scope.MainBody));
                Assert.That(scope.StatusColumn.parent, Is.EqualTo(scope.MainBody));
            }
        }

        [UnityTest]
        public IEnumerator RefreshForScreen_AppliesThreeColumnWidthProfilesAtResponsiveBreakpoints()
        {
            using (var scope = new ResponsiveLayoutTestScope())
            {
                scope.Controller.SetAnimationsEnabledForTests(false);

                scope.Controller.RefreshForScreen(1920f, 1080f);
                yield return null;

                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.FullThreeColumn));
                AssertColumnWidth(scope.TelemetryColumn, 280f, 280f);
                AssertColumnWidth(scope.ViewportColumn, 640f, 0f, 1f);
                AssertColumnWidth(scope.StatusColumn, 320f, 320f);

                scope.Controller.RefreshForScreen(1700f, 640f);
                yield return null;

                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
                AssertColumnWidth(scope.TelemetryColumn, 236f, 236f);
                AssertColumnWidth(scope.ViewportColumn, 640f, 0f, 1f);
                AssertColumnWidth(scope.StatusColumn, 260f, 260f);

                scope.Controller.RefreshForScreen(1280f, 623f);
                yield return null;

                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.Drawer));
                Assert.That(scope.TelemetryToggle.gameObject.activeSelf, Is.True);
                Assert.That(scope.StatusToggle.gameObject.activeSelf, Is.True);

                scope.Controller.RefreshForScreen(1296f, 656f);
                yield return null;

                Assert.That(scope.Controller.CurrentMode, Is.EqualTo(RuntimeUiLayoutMode.CompressedThreeColumn));
                AssertColumnWidth(scope.TelemetryColumn, 236f, 236f);
                AssertColumnWidth(scope.ViewportColumn, 640f, 0f, 1f);
                AssertColumnWidth(scope.StatusColumn, 260f, 260f);
            }
        }

        [UnityTest]
        public IEnumerator DrawerWithoutAnimation_MatchesAnimatedFinalState_AndReturnsFocus()
        {
            using (var immediate = new ResponsiveLayoutTestScope())
            using (var animated = new ResponsiveLayoutTestScope())
            {
                immediate.Controller.SetAnimationsEnabledForTests(false);
                animated.Controller.SetAnimationsEnabledForTests(true);

                immediate.Controller.RefreshForScreen(1279f, 720f);
                animated.Controller.RefreshForScreen(1279f, 720f);
                yield return null;

                EventSystem.current.SetSelectedGameObject(immediate.TelemetryToggle.gameObject);
                immediate.Controller.OpenSideDrawer(RuntimeUiSideDrawer.Telemetry);
                animated.Controller.OpenSideDrawer(RuntimeUiSideDrawer.Telemetry);
                yield return null;
                yield return new WaitForSecondsRealtime(0.2f);

                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(immediate.TelemetryInput.gameObject));
                AssertEquivalentDrawerState(immediate.TelemetryCanvasGroup, animated.TelemetryCanvasGroup);
                AssertEquivalentDrawerState(immediate.ScrimCanvasGroup, animated.ScrimCanvasGroup);
                Assert.That(immediate.ScrimImage.raycastTarget, Is.True);

                immediate.Controller.CloseSideDrawer();
                animated.Controller.CloseSideDrawer();
                yield return null;
                yield return new WaitForSecondsRealtime(0.2f);

                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(immediate.TelemetryToggle.gameObject));
                AssertEquivalentDrawerState(immediate.TelemetryCanvasGroup, animated.TelemetryCanvasGroup);
                AssertEquivalentDrawerState(immediate.ScrimCanvasGroup, animated.ScrimCanvasGroup);
                Assert.That(immediate.ScrimCanvasGroup.blocksRaycasts, Is.False);
            }
        }

        private static void AssertColumnWidth(RectTransform column, float minWidth, float preferredWidth, float flexibleWidth = 0f)
        {
            var layout = column.GetComponent<LayoutElement>();
            Assert.That(layout, Is.Not.Null, $"{column.name} must expose LayoutElement sizing");
            Assert.That(layout.minWidth, Is.EqualTo(minWidth).Within(0.1f), column.name + " minWidth");
            Assert.That(layout.preferredWidth, Is.EqualTo(preferredWidth).Within(0.1f), column.name + " preferredWidth");
            Assert.That(layout.flexibleWidth, Is.EqualTo(flexibleWidth).Within(0.1f), column.name + " flexibleWidth");
        }

        private static void AssertEquivalentDrawerState(CanvasGroup expected, CanvasGroup actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(expected.alpha, Is.EqualTo(actual.alpha).Within(0.01f));
            Assert.That(actual.interactable, Is.EqualTo(expected.interactable));
            Assert.That(actual.blocksRaycasts, Is.EqualTo(expected.blocksRaycasts));
        }

        private sealed class ResponsiveLayoutTestScope : System.IDisposable
        {
            private readonly GameObject root;
            private readonly GameObject eventSystemObject;

            public ResponsiveLayoutTestScope()
            {
                root = new GameObject("ResponsiveLayoutPlayModeScope");
                eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

                var runtimeRootObject = new GameObject("RuntimeUiRoot");
                runtimeRootObject.transform.SetParent(root.transform, false);
                var runtimeRoot = runtimeRootObject.AddComponent<RuntimeUiRoot>();

                var canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(runtimeRootObject.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var references = new RuntimeUiReferences();
                var uiRoot = CreateRect("UiRoot", canvasObject.transform);
                references.layout.systemBar = CreateRect("SystemBar", uiRoot);
                references.layout.configurationArea = CreateRect("ConfigurationArea", uiRoot);
                references.layout.mainBody = CreateRect("MainBody", uiRoot);
                references.layout.telemetryColumn = CreateRect("TelemetryColumn", references.layout.mainBody);
                references.layout.viewportColumn = CreateRect("ViewportColumn", references.layout.mainBody);
                references.layout.statusColumn = CreateRect("StatusColumn", references.layout.mainBody);
                references.layout.playbackBar = CreateRect("PlaybackBar", uiRoot);
                references.layout.drawerEntryLayer = CreateRect("DrawerEntryLayer", uiRoot);
                references.layout.telemetryDrawerToggle = CreateButton("TelemetryDrawerToggle", references.layout.drawerEntryLayer, out _);
                references.layout.statusDrawerToggle = CreateButton("StatusDrawerToggle", references.layout.drawerEntryLayer, out _);

                var modalRoot = CreateRect("ModalRoot", canvasObject.transform);
                references.layout.drawerScrim = CreateImage("DrawerScrim", modalRoot);
                CreateRect("OceanCurrentDrawer", modalRoot);
                CreateRect("FlightLegDrawer", modalRoot);

                TelemetryToggle = references.layout.telemetryDrawerToggle;
                StatusToggle = references.layout.statusDrawerToggle;
                MainBody = references.layout.mainBody;
                TelemetryColumn = references.layout.telemetryColumn;
                ViewportColumn = references.layout.viewportColumn;
                StatusColumn = references.layout.statusColumn;
                ScrimImage = references.layout.drawerScrim;

                TelemetryInput = AddSelectable("TelemetryField", references.layout.telemetryColumn, out _);
                AddSelectable("StatusField", references.layout.statusColumn, out _);

                runtimeRoot.ConfigureRuntimeReferences(canvas, modalRoot, references, RuntimeUiPanelFlags.None);

                var controllerObject = uiRoot.gameObject;
                Controller = controllerObject.AddComponent<ResponsiveUiLayoutController>();
                Controller.Bind(runtimeRoot, null);

            }

            public ResponsiveUiLayoutController Controller { get; }
            public Button TelemetryToggle { get; }
            public Button StatusToggle { get; }
            public RectTransform MainBody { get; }
            public RectTransform TelemetryColumn { get; }
            public RectTransform ViewportColumn { get; }
            public RectTransform StatusColumn { get; }
            public InputField TelemetryInput { get; }
            public Image ScrimImage { get; }
            public CanvasGroup TelemetryCanvasGroup => EnsureCanvasGroup(TelemetryColumn != null ? TelemetryColumn.gameObject : null);
            public CanvasGroup StatusCanvasGroup => EnsureCanvasGroup(StatusColumn != null ? StatusColumn.gameObject : null);
            public CanvasGroup ScrimCanvasGroup => EnsureCanvasGroup(ScrimImage != null ? ScrimImage.gameObject : null);

            public void Dispose()
            {
                if (root != null)
                {
                    Object.Destroy(root);
                }

                if (eventSystemObject != null)
                {
                    Object.Destroy(eventSystemObject);
                }
            }

            private static RectTransform CreateRect(string name, Transform parent)
            {
                var gameObject = new GameObject(name, typeof(RectTransform));
                gameObject.transform.SetParent(parent, false);
                return gameObject.GetComponent<RectTransform>();
            }

            private static Image CreateImage(string name, Transform parent)
            {
                var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
                gameObject.transform.SetParent(parent, false);
                return gameObject.GetComponent<Image>();
            }

            private static Button CreateButton(string name, Transform parent, out InputField nestedInput)
            {
                var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                gameObject.transform.SetParent(parent, false);
                nestedInput = AddSelectable(name + "NestedInput", gameObject.transform, out _);
                return gameObject.GetComponent<Button>();
            }

            private static InputField AddSelectable(string name, Transform parent, out Text text)
            {
                var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
                gameObject.transform.SetParent(parent, false);
                var textObject = new GameObject(name + "Text", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(gameObject.transform, false);
                text = textObject.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.text = string.Empty;
                var placeholderObject = new GameObject(name + "Placeholder", typeof(RectTransform), typeof(Text));
                placeholderObject.transform.SetParent(gameObject.transform, false);
                var placeholder = placeholderObject.GetComponent<Text>();
                placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                placeholder.text = name;
                var inputField = gameObject.GetComponent<InputField>();
                inputField.textComponent = text;
                inputField.placeholder = placeholder;
                return inputField;
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
        }
    }
}
