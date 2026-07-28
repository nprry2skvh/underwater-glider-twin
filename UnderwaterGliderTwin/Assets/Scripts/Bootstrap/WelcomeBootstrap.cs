using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnderwaterGliderTwin.Telemetry;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class WelcomeBootstrap : MonoBehaviour
    {
        private LaunchCoordinator coordinator;
        private InputField csvInput;
        private Text status;
        private Text notes;

        private void Awake()
        {
            coordinator = new LaunchCoordinator();
            var lastCsv = LaunchCoordinator.GetLastSuccessfulCsvPath();
            var request = LaunchRequestParser.Parse(Environment.GetCommandLineArgs(), lastCsv, SimulationProfile.Default);
            if (request.Mode != LaunchMode.Welcome && coordinator.ApplyAndLaunch(request)) return;
            BuildUi(lastCsv);
            if (request.HasErrors || !string.IsNullOrWhiteSpace(coordinator.LastError)) SetError(GetLaunchError(request));
        }

        private void BuildUi(string initialCsv)
        {
            var canvasObject = new GameObject("WelcomeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var background = CreateImage(canvas.transform, "Background", new Color(0.025f, 0.12f, 0.18f), Vector2.zero, Vector2.one);
            var panel = CreateImage(background.transform, "LaunchPanel", new Color(0.04f, 0.22f, 0.28f, 0.98f), new Vector2(.18f, .12f), new Vector2(.82f, .88f));
            CreateText(panel.transform, "Title", "水下滑翔机数字孪生", 34, new Vector2(.08f, .83f), new Vector2(.92f, .96f));
            CreateText(panel.transform, "Description", "CSV 遥测回放、参数化任务仿真与实验性短时预测", 18, new Vector2(.08f, .73f), new Vector2(.92f, .83f));
            csvInput = CreateInput(panel.transform, initialCsv, new Vector2(.08f, .58f), new Vector2(.72f, .67f));
            CreateButton(panel.transform, "ConfirmCsvButton", "确认 CSV 路径", new Vector2(.74f, .58f), new Vector2(.92f, .67f), ConfirmCsv);
            CreateButton(panel.transform, "StartCsvButton", "开始上次 / 默认 CSV", new Vector2(.08f, .43f), new Vector2(.48f, .53f), StartPreferredCsv);
            CreateButton(panel.transform, "SimulationButton", "进入仿真模式", new Vector2(.52f, .43f), new Vector2(.92f, .53f), () => coordinator.ApplyAndLaunch(coordinator.CreateSimulationRequest()));
            CreateButton(panel.transform, "NotesButton", "查看说明", new Vector2(.08f, .31f), new Vector2(.30f, .39f), () => notes.gameObject.SetActive(!notes.gameObject.activeSelf));
            notes = CreateText(panel.transform, "ProjectNotes", "CSV 数据要求使用 GBK 编码。预测功能仅用于可视化与开发验证，不得用于导航、安全或实际运行决策。", 15, new Vector2(.08f, .15f), new Vector2(.92f, .30f));
            notes.gameObject.SetActive(false);
            status = CreateText(panel.transform, "LaunchStatus", "", 15, new Vector2(.08f, .04f), new Vector2(.92f, .14f));
            status.color = new Color(1f, .55f, .55f);
        }

        private void ConfirmCsv()
        {
            var request = coordinator.CreateCsvRequest(csvInput.text);
            if (!coordinator.ApplyAndLaunch(request)) SetError(GetLaunchError(request));
        }

        private void StartPreferredCsv()
        {
            var path = LaunchCoordinator.GetLastSuccessfulCsvPath();
            try { if (string.IsNullOrWhiteSpace(path)) path = RuntimePathResolver.ResolveCsvPath(); }
            catch (Exception ex) { SetError(ex.Message); return; }
            var request = coordinator.CreateCsvRequest(path);
            if (!coordinator.ApplyAndLaunch(request)) SetError(GetLaunchError(request));
        }

        private void SetError(string message) { if (status != null) status.text = message; }

        private string GetLaunchError(LaunchRequest request)
        {
            if (!string.IsNullOrWhiteSpace(coordinator.LastError)) return coordinator.LastError;
            return request != null && request.HasErrors ? string.Join("\n", request.Errors) : "Launch request could not be started.";
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; return image;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.text = value; text.fontSize = size; text.color = Color.white; text.alignment = TextAnchor.MiddleLeft; return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction click)
        {
            var image = CreateImage(parent, name, new Color(.04f, .48f, .58f), min, max);
            var button = image.gameObject.AddComponent<Button>(); button.onClick.AddListener(click);
            var text = CreateText(button.transform, name + "Label", label, 16, Vector2.zero, Vector2.one); text.alignment = TextAnchor.MiddleCenter; return button;
        }

        private static InputField CreateInput(Transform parent, string value, Vector2 min, Vector2 max)
        {
            var image = CreateImage(parent, "CsvPathInput", new Color(.02f, .10f, .14f), min, max);
            var input = image.gameObject.AddComponent<InputField>();
            var text = CreateText(input.transform, "Text", value, 15, new Vector2(.03f, 0), new Vector2(.97f, 1)); input.textComponent = text;
            var placeholder = CreateText(input.transform, "Placeholder", "输入 CSV 文件绝对路径", 15, new Vector2(.03f, 0), new Vector2(.97f, 1)); placeholder.color = new Color(.6f, .7f, .72f); input.placeholder = placeholder;
            return input;
        }
    }
}
