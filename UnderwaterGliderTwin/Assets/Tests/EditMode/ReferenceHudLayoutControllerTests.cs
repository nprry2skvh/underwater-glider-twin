using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ReferenceHudLayoutControllerTests
    {
        [Test]
        public void Install_CollapsesPeripheralPanelsAndKeepsControlsVisible()
        {
            using (var scope = new UiTestObjectScope())
            {
                var root = scope.CreateRoot("RuntimeUI");
                var canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(root.transform, false);
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);

                var configuration = CreatePanel(canvasRect, "MissionConfigurationPanel", new Vector2(700f, 700f));
                var telemetry = CreatePanel(canvasRect, "TelemetryPanel", new Vector2(328f, 384f));
                var navigation = CreatePanel(canvasRect, "NavigationReferenceCard", new Vector2(328f, 172f));
                var status = CreatePanel(canvasRect, "MissionStatusPanel", new Vector2(352f, 574f));
                var playback = CreatePanel(canvasRect, "PlaybackControlsPanel", new Vector2(1540f, 124f));
                var viewport = CreatePanel(canvasRect, "OceanViewportFrame", new Vector2(1000f, 700f));
                var actionButton = UiFactory.Button("PlaybackActionButton", playback, "操作", Vector2.zero, new Vector2(90f, 30f));
                var primaryButton = UiFactory.Button("PlayPauseButton", playback, "开始", Vector2.zero, new Vector2(90f, 30f));
                var input = UiFactory.InputField("PlaybackValueInput", playback, "12", "数值", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120f, 30f));
                var graphiclessButton = new GameObject("GraphiclessButton", typeof(RectTransform), typeof(Button));
                graphiclessButton.transform.SetParent(playback, false);

                var controller = ReferenceHudLayoutController.Install(canvasObject);

                Assert.That(controller, Is.Not.Null);
                Assert.That(configuration.gameObject.activeSelf, Is.False);
                Assert.That(telemetry.gameObject.activeSelf, Is.False);
                Assert.That(navigation.gameObject.activeSelf, Is.False);
                Assert.That(status.gameObject.activeSelf, Is.False);
                Assert.That(playback.gameObject.activeSelf, Is.True);
                Assert.That(viewport.gameObject.activeSelf, Is.False);
                Assert.That(canvasRect.Find("HudDrawerTabLayer/HudConfigurationTab"), Is.Not.Null);
                Assert.That(canvasRect.Find("HudDrawerTabLayer/HudTelemetryTab"), Is.Not.Null);
                Assert.That(canvasRect.Find("HudDrawerTabLayer/HudStatusTab"), Is.Not.Null);
                Assert.That(canvasRect.Find("HudDrawerTabLayer/HudControlsTab"), Is.Not.Null);
                Assert.That(canvasRect.Find("HudDrawerTabLayer/QgcToolStrip"), Is.Not.Null);
                Assert.That(playback.GetComponent<Image>().color.r, Is.EqualTo(0.12f).Within(0.001f));
                Assert.That(actionButton.image.color, Is.EqualTo(Color.white));
                Assert.That(actionButton.colors.normalColor.r, Is.EqualTo(0.19f).Within(0.001f));
                Assert.That(primaryButton.image.color, Is.EqualTo(Color.white));
                Assert.That(primaryButton.colors.normalColor.r, Is.EqualTo(0.14f).Within(0.001f));
                Assert.That(input.targetGraphic.GetComponent<Image>().color.r, Is.LessThan(0.1f));

                var configurationTab = canvasRect.Find("HudDrawerTabLayer/HudConfigurationTab").GetComponent<RectTransform>();
                Assert.That(configurationTab.sizeDelta, Is.EqualTo(new Vector2(54f, 48f)));
                Assert.That(configurationTab.anchoredPosition, Is.EqualTo(new Vector2(12f, -68f)));
                Assert.That(configurationTab.GetComponentInChildren<Text>().text, Is.EqualTo("任务"));
                Assert.That(configurationTab.Find("QgcActiveIndicator"), Is.Not.Null);
            }
        }

        [Test]
        public void RightDrawerTabs_AreMutuallyExclusive()
        {
            using (var scope = new UiTestObjectScope())
            {
                var root = scope.CreateRoot("RuntimeUI");
                var canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(root.transform, false);
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                var telemetry = CreatePanel(canvasRect, "TelemetryPanel", new Vector2(328f, 384f));
                var navigation = CreatePanel(canvasRect, "NavigationReferenceCard", new Vector2(328f, 172f));
                var status = CreatePanel(canvasRect, "MissionStatusPanel", new Vector2(352f, 574f));

                var controller = ReferenceHudLayoutController.Install(canvasObject);
                controller.SetDrawerOpen(ReferenceHudLayoutController.TelemetryDrawerId, true);
                Assert.That(telemetry.gameObject.activeSelf, Is.True);
                Assert.That(navigation.gameObject.activeSelf, Is.True);
                Assert.That(status.gameObject.activeSelf, Is.False);

                controller.SetDrawerOpen(ReferenceHudLayoutController.StatusDrawerId, true);
                Assert.That(telemetry.gameObject.activeSelf, Is.False);
                Assert.That(navigation.gameObject.activeSelf, Is.False);
                Assert.That(status.gameObject.activeSelf, Is.True);
            }
        }

        [Test]
        public void TaskBar_UsesQGroundControlToolbarLayoutAndKeepsConfigurationAction()
        {
            using (var scope = new UiTestObjectScope())
            {
                var root = scope.CreateRoot("RuntimeUI");
                var canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(root.transform, false);
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);

                var header = UiFactory.EnsureCommandCenterHeader(canvasRect);
                var missionButton = UiFactory.Button("CommandCenterMissionConfigButton", header, "任务参数", Vector2.zero, new Vector2(112f, 28f));
                var configuration = CreatePanel(canvasRect, "MissionConfigurationPanel", new Vector2(700f, 700f));

                var controller = ReferenceHudLayoutController.Install(canvasObject);

                Assert.That(controller, Is.Not.Null);
                Assert.That(header.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(header.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(header.sizeDelta, Is.EqualTo(new Vector2(0f, 52f)));
                Assert.That(header.Find("QgcToolbarDecorations/QgcFlightViewLabel"), Is.Not.Null);
                Assert.That(header.Find("QgcToolbarDecorations/QgcFlightViewActiveLine"), Is.Not.Null);
                Assert.That(header.Find("QgcToolbarDecorations/QgcToolbarDivider"), Is.Not.Null);
                Assert.That(missionButton.image.color, Is.EqualTo(Color.white));
                Assert.That(missionButton.colors.normalColor.r, Is.EqualTo(0.14f).Within(0.001f));
                Assert.That(configuration.gameObject.activeSelf, Is.False);

                missionButton.onClick.Invoke();

                Assert.That(configuration.gameObject.activeSelf, Is.True);
            }
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return rect;
        }
    }
}
