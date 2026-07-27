using UnderwaterGliderTwin.Visualization;
using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public sealed class OceanCommandToolbarView : MonoBehaviour
    {
        private Text visibleArrowCountText;
        private OceanVolumeView oceanVolume;

        public void Initialize(TwinCameraController cameraController, TrajectoryView trajectoryView)
        {
            var canvas = UiFactory.EnsureCanvas(transform);
            var viewportFrame = UiFactory.Panel(
                "OceanViewportFrame",
                canvas.transform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -75f),
                new Vector2(-764f, -524f),
                new Color(0.01f, 0.08f, 0.14f, 0.025f));
            viewportFrame.GetComponent<Image>().raycastTarget = false;
            AddViewportBorder(viewportFrame);
            viewportFrame.SetAsFirstSibling();

            var panel = UiFactory.CommandPanel(
                "OceanCommandToolbar",
                canvas.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -320f),
                new Vector2(880f, 58f));

            UiFactory.Text("OceanToolbarTitle", panel, "3D \u6d77\u6d41\u573a\u53ef\u89c6\u5316", 16, TextAnchor.MiddleLeft, UiFactory.CommandText,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(166f, 32f));
            UiFactory.Text("OceanSpeedLegend", panel, "\u6d41\u901f", 11, TextAnchor.MiddleLeft, new Color(0.55f, 0.82f, 0.94f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(184f, 10f), new Vector2(38f, 20f));
            UiFactory.Panel("OceanLegendLow", panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(224f, 0f), new Vector2(26f, 6f), new Color(0.08f, 0.42f, 1f, 1f));
            UiFactory.Panel("OceanLegendMid", panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(250f, 0f), new Vector2(26f, 6f), new Color(0.08f, 0.95f, 0.72f, 1f));
            UiFactory.Panel("OceanLegendHigh", panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(276f, 0f), new Vector2(26f, 6f), new Color(1f, 0.76f, 0.18f, 1f));
            visibleArrowCountText = UiFactory.Text("OceanVisibleArrowCount", panel, "\u6709\u6548 0 / 360", 11, TextAnchor.MiddleLeft, new Color(0.62f, 0.86f, 0.92f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(184f, -11f), new Vector2(120f, 18f));

            AddViewButton(panel, "CameraFollowCommand", "\u8ddf\u968f", 328f, CameraMode.Follow, cameraController, trajectoryView);
            AddViewButton(panel, "CameraGlobalCommand", "\u5168\u5c40", 396f, CameraMode.Global, cameraController, trajectoryView);
            AddViewButton(panel, "CameraTopCommand", "\u4fef\u89c6", 464f, CameraMode.Top, cameraController, trajectoryView);
            AddViewButton(panel, "CameraSideCommand", "\u4fa7\u89c6", 532f, CameraMode.Side, cameraController, trajectoryView);
            AddViewButton(panel, "CameraOrbitCommand", "\u73af\u7ed5", 600f, CameraMode.Orbit, cameraController, trajectoryView);
            var reset = UiFactory.PrimaryButton("CameraResetCommand", panel, "\u590d\u4f4d\u89c6\u89d2", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(754f, 0f), new Vector2(104f, 28f));
            reset.onClick.AddListener(() =>
            {
                cameraController.ResetView();
                trajectoryView.SetCameraMode(CameraMode.Global);
            });
        }

        private void Update()
        {
            if (visibleArrowCountText == null)
            {
                return;
            }

            if (oceanVolume == null)
            {
                oceanVolume = FindObjectOfType<OceanVolumeView>();
            }

            var visibleCount = oceanVolume != null ? oceanVolume.VisibleCurrentArrowCount : 0;
            visibleArrowCountText.text = $"\u6709\u6548 {visibleCount} / {OceanVolumeSamplingCache.MaximumVisibleArrowCount}";
        }

        private static void AddViewButton(Transform parent, string name, string label, float x, CameraMode mode, TwinCameraController cameraController, TrajectoryView trajectoryView)
        {
            var button = UiFactory.Button(name, parent, label, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(60f, 28f));
            button.onClick.AddListener(() =>
            {
                cameraController.SetMode(mode);
                trajectoryView.SetCameraMode(mode);
            });
        }

        private static void AddViewportBorder(RectTransform frame)
        {
            var color = new Color(0.06f, 0.74f, 1f, 0.74f);
            AddBorderLine("OceanViewportBorderTop", frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 1f), color);
            AddBorderLine("OceanViewportBorderBottom", frame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 1f), color);
            AddBorderLine("OceanViewportBorderLeft", frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1f, 0f), color);
            AddBorderLine("OceanViewportBorderRight", frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(1f, 0f), color);
        }

        private static void AddBorderLine(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var line = UiFactory.Panel(name, parent, anchorMin, anchorMax, pivot, position, size, color);
            line.GetComponent<Image>().raycastTarget = false;
        }
    }
}
