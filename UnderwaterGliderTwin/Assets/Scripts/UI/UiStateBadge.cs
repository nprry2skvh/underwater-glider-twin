using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public enum UiStateKind
    {
        Normal,
        Standby,
        Prediction,
        Warning,
        Error
    }

    [ExecuteAlways]
    public sealed class UiStateBadge : MonoBehaviour
    {
        [SerializeField] private Text stateText;
        [SerializeField] private Image stateMarker;
        [SerializeField] private Image stateBar;
        [SerializeField] private UiStateKind state = UiStateKind.Standby;

        public UiStateKind State => state;
        public string Label => stateText != null ? stateText.text : string.Empty;
        public Text StateText => stateText;
        public Image MarkerImage => stateMarker;
        public Image StateBar => stateBar;

        private void Awake()
        {
            EnsureVisuals();
            SetState(state, Label);
        }

        public void SetState(UiStateKind nextState, string label)
        {
            EnsureVisuals();
            state = nextState;
            stateText.text = string.IsNullOrWhiteSpace(label) ? GetDefaultLabel(nextState) : label;
            stateText.raycastTarget = false;
            stateMarker.color = GetStateColor(nextState);
            stateBar.color = GetStateColor(nextState);
            stateMarker.raycastTarget = false;
            stateBar.raycastTarget = false;
        }

        private void EnsureVisuals()
        {
            var root = transform as RectTransform;
            if (root == null)
            {
                root = gameObject.AddComponent<RectTransform>();
            }

            if (stateBar == null)
            {
                var barObject = new GameObject("StateBar", typeof(RectTransform), typeof(Image));
                barObject.transform.SetParent(root, false);
                stateBar = barObject.GetComponent<Image>();
                var barRect = barObject.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(0f, 1f);
                barRect.pivot = new Vector2(0f, 0.5f);
                barRect.sizeDelta = new Vector2(3f, 0f);
            }

            if (stateMarker == null)
            {
                var markerObject = new GameObject("StateMarker", typeof(RectTransform), typeof(Image));
                markerObject.transform.SetParent(root, false);
                stateMarker = markerObject.GetComponent<Image>();
                var markerRect = markerObject.GetComponent<RectTransform>();
                markerRect.anchorMin = new Vector2(0f, 0.5f);
                markerRect.anchorMax = new Vector2(0f, 0.5f);
                markerRect.pivot = new Vector2(0f, 0.5f);
                markerRect.anchoredPosition = new Vector2(10f, 0f);
                markerRect.sizeDelta = new Vector2(8f, 8f);
            }

            if (stateText == null)
            {
                var textObject = new GameObject("StateText", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(root, false);
                stateText = textObject.GetComponent<Text>();
                stateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                stateText.fontSize = 14;
                stateText.alignment = TextAnchor.MiddleLeft;
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0f, 0f);
                textRect.anchorMax = new Vector2(1f, 1f);
                textRect.offsetMin = new Vector2(24f, 0f);
                textRect.offsetMax = new Vector2(-4f, 0f);
            }

            stateMarker.raycastTarget = false;
            stateBar.raycastTarget = false;
            stateText.raycastTarget = false;
        }

        private static string GetDefaultLabel(UiStateKind value)
        {
            switch (value)
            {
                case UiStateKind.Normal: return "正常";
                case UiStateKind.Standby: return "待机";
                case UiStateKind.Prediction: return "预测";
                case UiStateKind.Warning: return "警告";
                case UiStateKind.Error: return "错误";
                default: return "状态";
            }
        }

        private static Color GetStateColor(UiStateKind value)
        {
            switch (value)
            {
                case UiStateKind.Normal: return UiFactory.CommandSuccess;
                case UiStateKind.Prediction: return UiFactory.CommandWarning;
                case UiStateKind.Warning: return UiFactory.CommandWarning;
                case UiStateKind.Error: return UiFactory.CommandError;
                default: return UiFactory.CommandMutedText;
            }
        }
    }
}
