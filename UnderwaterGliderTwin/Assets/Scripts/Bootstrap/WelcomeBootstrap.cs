using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnderwaterGliderTwin.Telemetry;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Bootstrap
{
    public sealed class WelcomeBootstrap : MonoBehaviour
    {
        [SerializeField] private Canvas welcomeCanvas;
        [SerializeField] private InputField csvInput;
        [SerializeField] private Text status;
        [SerializeField] private Button confirmCsvButton;
        [SerializeField] private Button startCsvButton;
        [SerializeField] private Button simulationButton;
        [SerializeField] private bool allowRuntimeFallback;

        private LaunchCoordinator coordinator;
        private static Font welcomeFont;

        public List<UiReferenceIssue> ValidateReferences()
        {
            var issues = new List<UiReferenceIssue>();
            UiReferenceValidator.Require(welcomeCanvas, this, "Welcome.unity", "welcomeCanvas", issues);
            UiReferenceValidator.Require(csvInput, this, "Welcome.unity", "csvInput", issues);
            UiReferenceValidator.Require(status, this, "Welcome.unity", "status", issues);
            UiReferenceValidator.Require(confirmCsvButton, this, "Welcome.unity", "confirmCsvButton", issues);
            UiReferenceValidator.Require(startCsvButton, this, "Welcome.unity", "startCsvButton", issues);
            UiReferenceValidator.Require(simulationButton, this, "Welcome.unity", "simulationButton", issues);
            return issues;
        }

        private void Awake()
        {
            coordinator = new LaunchCoordinator();
            var lastCsv = LaunchCoordinator.GetLastSuccessfulCsvPath();
            var request = LaunchRequestParser.Parse(Environment.GetCommandLineArgs(), lastCsv, SimulationProfile.Default);
            if (request.Mode != LaunchMode.Welcome && coordinator.ApplyAndLaunch(request))
            {
                return;
            }

            var issues = ValidateReferences();
            if (issues.Count > 0)
            {
                if (!allowRuntimeFallback)
                {
                    foreach (var issue in issues)
                    {
                        Debug.LogError(issue.ToString(), this);
                    }

                    enabled = false;
                    return;
                }

                RuntimeUiFallback.AllowRuntimeFallback = true;
                RuntimeUiFallback.LogFallback("WelcomeCanvas");
                BuildUi(lastCsv);
            }
            else
            {
                BindUi(lastCsv);
            }

            if (request.HasErrors || !string.IsNullOrWhiteSpace(coordinator.LastError))
            {
                SetError(GetLaunchError(request));
            }
        }

        private void BindUi(string initialCsv)
        {
            csvInput.text = initialCsv ?? string.Empty;
            status.text = string.Empty;
            confirmCsvButton.onClick.RemoveAllListeners();
            confirmCsvButton.onClick.AddListener(ConfirmCsv);
            startCsvButton.onClick.RemoveAllListeners();
            startCsvButton.onClick.AddListener(StartPreferredCsv);
            simulationButton.onClick.RemoveAllListeners();
            simulationButton.onClick.AddListener(() => coordinator.ApplyAndLaunch(coordinator.CreateSimulationRequest()));
        }

        private void BuildUi(string initialCsv)
        {
            var canvasObject = new GameObject("WelcomeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            welcomeCanvas = canvasObject.GetComponent<Canvas>();
            welcomeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            welcomeCanvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            welcomeCanvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
            if (FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            var background = CreateImage(canvasObject.transform, "BackgroundImage", new Color(0.025f, 0.12f, 0.18f), Vector2.zero, Vector2.one);
            var panel = CreateImage(background.transform, "LaunchPanel", new Color(0.03f, 0.12f, 0.22f, 0.98f), new Vector2(.10f, .08f), new Vector2(.90f, .92f));
            CreateText(panel.transform, "TitleText", "Underwater Glider Digital Twin", 34, new Vector2(.08f, .83f), new Vector2(.92f, .96f));
            CreateText(panel.transform, "DescriptionText", "CSV replay, simulation, and short-horizon prediction", 18, new Vector2(.08f, .73f), new Vector2(.92f, .83f));
            csvInput = CreateInput(panel.transform, "CsvPathInput", initialCsv, new Vector2(.08f, .58f), new Vector2(.72f, .67f));
            confirmCsvButton = CreateButton(panel.transform, "ConfirmCsvButton", "Confirm CSV Path", new Vector2(.74f, .58f), new Vector2(.92f, .67f));
            startCsvButton = CreateButton(panel.transform, "StartCsvButton", "Start Previous / Default CSV", new Vector2(.08f, .43f), new Vector2(.48f, .53f));
            simulationButton = CreateButton(panel.transform, "SimulationButton", "Enter Simulation Mode", new Vector2(.52f, .43f), new Vector2(.92f, .53f));
            status = CreateText(panel.transform, "LaunchStatusText", string.Empty, 15, new Vector2(.08f, .18f), new Vector2(.92f, .30f));
            BindUi(initialCsv);
        }

        private void ConfirmCsv()
        {
            var request = coordinator.CreateCsvRequest(csvInput.text);
            if (!coordinator.ApplyAndLaunch(request))
            {
                SetError(GetLaunchError(request));
            }
        }

        private void StartPreferredCsv()
        {
            var path = LaunchCoordinator.GetLastSuccessfulCsvPath();
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = RuntimePathResolver.ResolveCsvPath();
                }
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                return;
            }

            var request = coordinator.CreateCsvRequest(path);
            if (!coordinator.ApplyAndLaunch(request))
            {
                SetError(GetLaunchError(request));
            }
        }

        private void SetError(string message)
        {
            if (status != null)
            {
                status.text = message;
            }
        }

        private string GetLaunchError(LaunchRequest request)
        {
            if (!string.IsNullOrWhiteSpace(coordinator.LastError))
            {
                return coordinator.LastError;
            }

            return request != null && request.HasErrors
                ? string.Join("\n", request.Errors)
                : "Launch request could not be started.";
        }

        private void OnDestroy()
        {
            RuntimeUiFallback.Reset();
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go.GetComponent<Image>();
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = GetWelcomeFont();
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static Font GetWelcomeFont()
        {
            if (welcomeFont != null)
            {
                return welcomeFont;
            }

            welcomeFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "Arial" }, 32);
            return welcomeFont != null ? welcomeFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            var image = CreateImage(parent, name, new Color(.04f, .48f, .58f), min, max);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText(button.transform, name + "Label", label, 16, Vector2.zero, Vector2.one);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static InputField CreateInput(Transform parent, string name, string value, Vector2 min, Vector2 max)
        {
            var image = CreateImage(parent, name, new Color(.02f, .10f, .14f), min, max);
            var input = image.gameObject.AddComponent<InputField>();
            var text = CreateText(input.transform, "Text", value, 15, new Vector2(.03f, 0), new Vector2(.97f, 1));
            input.textComponent = text;
            var placeholder = CreateText(input.transform, "Placeholder", "Enter CSV absolute path", 15, new Vector2(.03f, 0), new Vector2(.97f, 1));
            placeholder.color = new Color(.6f, .7f, .72f);
            input.placeholder = placeholder;
            return input;
        }
    }
}
