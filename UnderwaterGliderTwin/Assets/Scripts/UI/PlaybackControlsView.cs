using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.Playback;
using UnderwaterGliderTwin.Visualization;

namespace UnderwaterGliderTwin.UI
{
    public sealed class PlaybackControlsView : MonoBehaviour
    {
        private PlaybackController playback;
        private Button playPauseButton;
        private Slider progressSlider;
        private Text statusText;
        private bool updatingSlider;
        private Action exitAction;
        private Action missionViewAction;
        private Func<string> screenshotAction;
        private readonly Dictionary<float, Button> speedButtons = new Dictionary<float, Button>();

        [System.Obsolete("Use Bind(...) with editable UI references.")]
        public void Initialize(PlaybackController playbackController, TwinCameraController cameraController, UnderwaterEnvironmentBuilder environmentBuilder, TrajectoryView trajectoryView, Action onExitRequested = null, Func<string> onScreenshotRequested = null, Action onMissionViewRequested = null)
        {
            playback = playbackController;
            exitAction = onExitRequested;
            screenshotAction = onScreenshotRequested;
            missionViewAction = onMissionViewRequested;
            var canvas = UiFactory.EnsureCanvas(transform);
            var panel = UiFactory.CommandPanel("PlaybackControlsPanel", canvas.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(-100f, 124f));
            var groupColor = new Color(0.42f, 0.76f, 0.84f);
            UiFactory.Text("PlaybackGroupLabel", panel, "回放控制", 11, TextAnchor.MiddleLeft, groupColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(120f, 18f));

            playPauseButton = UiFactory.PrimaryButton("PlayPauseButton", panel, "开始", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -40f), new Vector2(92f, 32f));
            playPauseButton.onClick.AddListener(OnPlayPauseClicked);
            UiFactory.Button("ReverseButton", panel, "倒放", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(118f, -40f), new Vector2(92f, 32f)).onClick.AddListener(OnReverseClicked);
            UiFactory.Button("ReplayButton", panel, "回放", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(218f, -40f), new Vector2(92f, 32f)).onClick.AddListener(OnReplayClicked);
            UiFactory.Button("ResetButton", panel, "重置", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(318f, -40f), new Vector2(92f, 32f)).onClick.AddListener(OnResetClicked);
            UiFactory.Button("ExportButton", panel, "导出", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(418f, -40f), new Vector2(92f, 32f)).onClick.AddListener(OnExportClicked);

            progressSlider = UiFactory.Slider("ProgressSlider", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(528f, -40f), new Vector2(746f, 32f));
            progressSlider.onValueChanged.AddListener(OnSliderChanged);

            UiFactory.Button("ExitButton", panel, "退出", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -40f), new Vector2(88f, 32f)).onClick.AddListener(OnExitClicked);

            UiFactory.Text("ViewGroupLabel", panel, "视图与图层", 11, TextAnchor.MiddleLeft, groupColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -70f), new Vector2(160f, 18f));
            UiFactory.Button("CameraFollowButton", panel, "跟随", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -94f), new Vector2(88f, 28f)).onClick.AddListener(() => SetCameraMode(cameraController, trajectoryView, CameraMode.Follow));
            UiFactory.Button("CameraGlobalButton", panel, "全局", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(114f, -94f), new Vector2(88f, 28f)).onClick.AddListener(() => SetCameraMode(cameraController, trajectoryView, CameraMode.Global));
            UiFactory.Button("CameraOrbitButton", panel, "环绕", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -94f), new Vector2(88f, 28f)).onClick.AddListener(() => SetCameraMode(cameraController, trajectoryView, CameraMode.Orbit));
            UiFactory.Button("MissionVolumeButton", panel, "海域", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(306f, -94f), new Vector2(72f, 28f)).onClick.AddListener(OnMissionViewClicked);

            UiFactory.Toggle("FogToggle", panel, "雾效", true, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(322f, -94f), new Vector2(84f, 26f)).onValueChanged.AddListener(environmentBuilder.SetFogEnabled);
            UiFactory.Toggle("ParticlesToggle", panel, "粒子", true, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(414f, -94f), new Vector2(110f, 26f)).onValueChanged.AddListener(environmentBuilder.SetParticlesEnabled);
            var trajectoryToggle = UiFactory.Toggle("TrajectoryToggle", panel, "航迹", true, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(538f, -94f), new Vector2(128f, 26f));
            trajectoryToggle.onValueChanged.AddListener(trajectoryView.SetVisible);

            UiFactory.Text("SpeedGroupLabel", panel, "速度", 11, TextAnchor.MiddleLeft, groupColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(694f, -70f), new Vector2(100f, 18f));
            AddSpeedButton(panel, "Speed05Button", "0.5x", 694f, 0.5f);
            AddSpeedButton(panel, "Speed1Button", "1x", 752f, 1f);
            AddSpeedButton(panel, "Speed2Button", "2x", 810f, 2f);
            AddSpeedButton(panel, "Speed5Button", "5x", 868f, 5f);
            AddSpeedButton(panel, "Speed10Button", "10x", 926f, 10f);

            UiFactory.Text("LegendActual", panel, "实际", 12, TextAnchor.MiddleLeft, new Color(0.18f, 0.72f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1048f, -94f), new Vector2(64f, 20f));
            UiFactory.Text("LegendPredicted", panel, "预测历史", 12, TextAnchor.MiddleLeft, new Color(1f, 0.84f, 0.2f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1122f, -94f), new Vector2(96f, 20f));
            UiFactory.Text("LegendPlanned", panel, "计划", 12, TextAnchor.MiddleLeft, new Color(0.3f, 0.92f, 0.52f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1210f, -94f), new Vector2(66f, 20f));
            statusText = UiFactory.Text("PlaybackStatus", panel, "回放已就绪", 12, TextAnchor.MiddleRight, new Color(0.78f, 0.96f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -94f), new Vector2(490f, 20f));
            UiFactory.ConfigureWrappedStatusText(statusText);

            trajectoryView.SetCameraMode(CameraMode.Follow);
            trajectoryView.SetVisible(true);
            HideLegacyCameraControls(panel);
            ConfigureResponsiveLayout(panel);

            playback.FrameChanged += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01);
        }

        public void Bind(PlaybackControlsRefs refs, PlaybackController playbackController, TwinCameraController cameraController, UnderwaterEnvironmentBuilder environmentBuilder, TrajectoryView trajectoryView, Action onExitRequested = null, Func<string> onScreenshotRequested = null, Action onMissionViewRequested = null)
        {
            playback = playbackController;
            exitAction = onExitRequested;
            screenshotAction = onScreenshotRequested;
            missionViewAction = onMissionViewRequested;
            if (refs == null || playback == null || playback.Model == null)
            {
                return;
            }

            playPauseButton = refs.playPauseButton;
            progressSlider = refs.progressSlider;
            statusText = refs.statusText;
            if (statusText != null)
            {
                // Keep the configured Prefab reference, while normalizing its runtime name
                // to the generated-ui contract used by the responsive row binder.
                statusText.gameObject.name = "PlaybackStatus";
                statusText.text = "回放已就绪";
            }
            UiFactory.ConfigureWrappedStatusText(statusText);
            BindButton(refs.playPauseButton, OnPlayPauseClicked);
            BindButton(refs.reverseButton, OnReverseClicked);
            BindButton(refs.replayButton, OnReplayClicked);
            BindButton(refs.resetButton, OnResetClicked);
            BindButton(refs.exportButton, OnExportClicked);
            BindButton(refs.exitButton, OnExitClicked);
            BindButton(refs.missionVolumeButton, OnMissionViewClicked);
            HideLegacyCameraControls(refs.panel);
            ConfigureResponsiveLayout(refs.panel);
            BindCameraButton(refs.cameraFollowButton, cameraController, trajectoryView, CameraMode.Follow);
            BindCameraButton(refs.cameraGlobalButton, cameraController, trajectoryView, CameraMode.Global);
            BindCameraButton(refs.cameraOrbitButton, cameraController, trajectoryView, CameraMode.Orbit);
            if (refs.fogToggle != null && environmentBuilder != null)
            {
                refs.fogToggle.onValueChanged.RemoveAllListeners();
                refs.fogToggle.onValueChanged.AddListener(environmentBuilder.SetFogEnabled);
            }

            if (refs.particlesToggle != null && environmentBuilder != null)
            {
                refs.particlesToggle.onValueChanged.RemoveAllListeners();
                refs.particlesToggle.onValueChanged.AddListener(environmentBuilder.SetParticlesEnabled);
            }

            if (refs.trajectoryToggle != null && trajectoryView != null)
            {
                refs.trajectoryToggle.onValueChanged.RemoveAllListeners();
                refs.trajectoryToggle.onValueChanged.AddListener(trajectoryView.SetVisible);
            }

            speedButtons.Clear();
            BindSpeedButton(refs.speed05Button, 0.5f);
            BindSpeedButton(refs.speed1Button, 1f);
            BindSpeedButton(refs.speed2Button, 2f);
            BindSpeedButton(refs.speed5Button, 5f);
            BindSpeedButton(refs.speed10Button, 10f);
            if (progressSlider != null)
            {
                progressSlider.onValueChanged.RemoveAllListeners();
                progressSlider.onValueChanged.AddListener(OnSliderChanged);
            }

            playback.FrameChanged -= OnFrameChanged;
            playback.FrameChanged += OnFrameChanged;
            OnFrameChanged(playback.Model.CurrentFrame, playback.Model.CurrentIndex, playback.Model.Progress01);
        }

        private void OnDestroy()
        {
            if (playback != null)
            {
                playback.FrameChanged -= OnFrameChanged;
            }
        }

        private static void HideLegacyCameraControls(Transform panel)
        {
            var legacyNames = new[]
            {
                "ViewGroupLabel",
                "CameraFollowButton",
                "CameraGlobalButton",
                "CameraOrbitButton"
            };
            foreach (var legacyName in legacyNames)
            {
                var legacyControl = panel.Find(legacyName);
                if (legacyControl != null)
                {
                    legacyControl.gameObject.SetActive(false);
                }
            }
        }

        private static void ConfigureResponsiveLayout(Transform panel)
        {
            if (panel == null)
            {
                return;
            }

            var operations = EnsureLayoutRow(panel, "PlaybackOperationsRow", new Vector2(0f, 0.66f), Vector2.one, UiVisualRole.CardFill);
            var timeline = EnsureLayoutRow(panel, "PlaybackTimelineRow", new Vector2(0f, 0.34f), new Vector2(1f, 0.66f), UiVisualRole.InputFill);
            var options = EnsureLayoutRow(panel, "PlaybackOptionsRow", Vector2.zero, new Vector2(1f, 0.34f), UiVisualRole.CardFill);

            MoveToRow(panel, operations, "PlaybackGroupLabel", 86f, false, "TitleText");
            MoveToRow(panel, operations, "PlayPauseButton", 82f);
            MoveToRow(panel, operations, "ReverseButton", 82f);
            MoveToRow(panel, operations, "ReplayButton", 82f);
            MoveToRow(panel, operations, "ResetButton", 82f);
            MoveToRow(panel, operations, "ExportButton", 82f);
            MoveToRow(panel, operations, "ExitButton", 76f);

            MoveToRow(panel, timeline, "ProgressSlider", 0f, true);
            MoveToRow(panel, timeline, "PlaybackStatus", 220f);

            MoveToRow(panel, options, "MissionVolumeButton", 76f);
            MoveToRow(panel, options, "FogToggle", 76f);
            MoveToRow(panel, options, "ParticlesToggle", 76f);
            MoveToRow(panel, options, "TrajectoryToggle", 76f);
            MoveToRow(panel, options, "SpeedGroupLabel", 48f);
            MoveToRow(panel, options, "Speed05Button", 48f);
            MoveToRow(panel, options, "Speed1Button", 48f);
            MoveToRow(panel, options, "Speed2Button", 48f);
            MoveToRow(panel, options, "Speed5Button", 48f);
            MoveToRow(panel, options, "Speed10Button", 48f);
            MoveToRow(panel, options, "LegendActual", 64f);
            MoveToRow(panel, options, "LegendPredicted", 96f);
            MoveToRow(panel, options, "LegendPlanned", 66f);

            UiFactory.EnsureDivider(operations, "PlaybackOperationsDivider", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 0f), new Vector2(-8f, 1f));
            UiFactory.EnsureDivider(timeline, "PlaybackTimelineDivider", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 0f), new Vector2(-8f, 1f));

            ApplyButtonRole(panel, "PlayPauseButton", UiButtonRole.Primary);
            ApplyButtonRole(panel, "ReverseButton", UiButtonRole.Secondary);
            ApplyButtonRole(panel, "ReplayButton", UiButtonRole.Secondary);
            ApplyButtonRole(panel, "ResetButton", UiButtonRole.Secondary);
            ApplyButtonRole(panel, "ExportButton", UiButtonRole.Secondary);
            ApplyButtonRole(panel, "ExitButton", UiButtonRole.Quiet);
            ApplyButtonRole(panel, "MissionVolumeButton", UiButtonRole.Secondary);
            UiFactory.ApplyRuntimePalette(panel);
        }

        private static RectTransform EnsureLayoutRow(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, UiVisualRole surfaceRole)
        {
            var row = parent.Find(name) as RectTransform;
            if (row == null)
            {
                row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(parent, false);
            }

            row.anchorMin = anchorMin;
            row.anchorMax = anchorMax;
            row.offsetMin = new Vector2(12f, 0f);
            row.offsetMax = new Vector2(-12f, 0f);
            row.pivot = new Vector2(0.5f, 0.5f);
            var rowImage = row.GetComponent<Image>();
            if (rowImage == null)
            {
                rowImage = row.gameObject.AddComponent<Image>();
                UiFactory.ApplyCommandPalette(rowImage, surfaceRole);
            }
            rowImage.raycastTarget = false;
            var layout = row.GetComponent<HorizontalLayoutGroup>() ?? row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return row;
        }

        private static void ApplyButtonRole(Transform panel, string name, UiButtonRole role)
        {
            var control = FindDescendant(panel, name);
            UiFactory.ApplyButtonRole(control != null ? control.GetComponent<Button>() : null, role);
        }

        private static void MoveToRow(Transform panel, Transform row, string name, float preferredWidth, bool flexible = false, string alias = null)
        {
            var control = FindDescendant(panel, name);
            if (control == null && !string.IsNullOrWhiteSpace(alias))
            {
                control = FindDescendant(panel, alias);
            }
            if (control == null || row == null)
            {
                return;
            }

            if (CanReparentConfiguredTransform(control))
            {
                control.SetParent(row, false);
            }
            control.anchorMin = Vector2.one;
            control.anchorMax = Vector2.one;
            control.offsetMin = Vector2.zero;
            control.offsetMax = Vector2.zero;
            var element = control.GetComponent<LayoutElement>() ?? control.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 30f;
            element.preferredWidth = preferredWidth;
            if (name == "PlaybackStatus")
            {
                var status = control.GetComponent<Text>();
                if (status != null)
                {
                    status.verticalOverflow = VerticalWrapMode.Truncate;
                }

                element.minHeight = 24f;
                element.preferredHeight = 24f;
            }
            element.minWidth = flexible ? 80f : preferredWidth;
            element.flexibleWidth = flexible ? 1f : 0f;
            ConfigureControlText(control);
        }

        private static bool CanReparentConfiguredTransform(Transform target)
        {
#if UNITY_EDITOR
            return target == null || Application.isPlaying || !UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target);
#else
            return true;
#endif
        }

        private static void ConfigureControlText(RectTransform control)
        {
            foreach (var text in control.GetComponentsInChildren<Text>(true))
            {
                if (text.transform != control && text.transform.parent != control)
                {
                    continue;
                }

                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(4f, 0f);
                text.rectTransform.offsetMax = new Vector2(-4f, 0f);
                text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                UiFactory.ApplyTextRole(text, UiTextRole.Button, RuntimeUiLayoutMode.CompressedThreeColumn);
            }
        }

        private static RectTransform FindDescendant(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child as RectTransform;
                }
            }

            return null;
        }

        private void AddSpeedButton(Transform panel, string name, string label, float x, float speed)
        {
            var button = UiFactory.Button(name, panel, label, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -94f), new Vector2(50f, 28f));
            speedButtons[speed] = button;
            button.onClick.AddListener(() =>
            {
                playback.SetSpeed(speed);
                RefreshButtonLabels();
                SetStatus($"回放速度：{speed:0.##}x");
            });
        }

        private void OnPlayPauseClicked()
        {
            playback.TogglePlaying();
            RefreshButtonLabels();
            SetStatus(playback.Model.IsPlaying ? "正在回放" : "回放已暂停");
        }

        private void OnReverseClicked()
        {
            playback.PlayReverse();
            RefreshButtonLabels();
            SetStatus("正在倒放");
        }

        private void OnReplayClicked()
        {
            playback.Restart(true);
            RefreshButtonLabels();
            SetStatus("已从任务起点重新回放");
        }

        private void OnResetClicked()
        {
            playback.Restart(false);
            RefreshButtonLabels();
            SetStatus("已重置到任务起点");
        }

        private void OnExportClicked()
        {
            if (screenshotAction == null)
            {
                SetStatus("截图功能不可用");
                return;
            }

            var exportPath = screenshotAction.Invoke();
            SetStatus($"截图已保存：{exportPath}");
        }

        private void OnMissionViewClicked()
        {
            missionViewAction?.Invoke();
            SetStatus("已切换到局部三维海域视角");
        }

        private void OnExitClicked()
        {
            if (exitAction != null)
            {
                exitAction.Invoke();
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }

        private void OnSliderChanged(float value)
        {
            if (updatingSlider)
            {
                return;
            }

            playback.Seek(value);
        }

        private void OnFrameChanged(UnderwaterGliderTwin.Telemetry.TelemetryFrame frame, int index, float progress01)
        {
            updatingSlider = true;
            if (progressSlider != null)
            {
                progressSlider.value = progress01;
            }
            updatingSlider = false;
            RefreshButtonLabels();
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void BindSpeedButton(Button button, float speed)
        {
            if (button == null)
            {
                return;
            }

            speedButtons[speed] = button;
            BindButton(button, () =>
            {
                playback.SetSpeed(speed);
                RefreshButtonLabels();
                SetStatus($"speed {speed:0.##}x");
            });
        }

        private static void BindCameraButton(Button button, TwinCameraController cameraController, TrajectoryView trajectoryView, CameraMode mode)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                cameraController?.SetMode(mode);
                trajectoryView?.SetCameraMode(mode);
            });
        }

        private void RefreshButtonLabels()
        {
            UiFactory.SetButtonText(playPauseButton, playback != null && playback.Model.IsPlaying ? "暂停" : "开始");
            foreach (var entry in speedButtons)
            {
                UiFactory.ApplyCommandPalette(
                    entry.Value.image,
                    playback != null && Mathf.Approximately(playback.Model.Speed, entry.Key)
                        ? UiVisualRole.AccentFill
                        : UiVisualRole.ButtonFill);
                var label = entry.Value.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    UiFactory.ApplyCommandPalette(
                        label,
                        playback != null && Mathf.Approximately(playback.Model.Speed, entry.Key)
                            ? UiVisualRole.AccentText
                            : UiVisualRole.Text);
                }
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                UiFactory.ConfigureWrappedStatusText(statusText);
            }
        }

        private static void SetCameraMode(TwinCameraController cameraController, TrajectoryView trajectoryView, CameraMode mode)
        {
            cameraController.SetMode(mode);
            trajectoryView.SetCameraMode(mode);
        }
    }
}
