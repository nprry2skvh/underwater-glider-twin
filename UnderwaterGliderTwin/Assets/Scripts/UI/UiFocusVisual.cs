using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    [ExecuteAlways]
    public sealed class UiFocusVisual : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Selectable host;
        [SerializeField] private Outline outline;
        [SerializeField] private Image focusDecoration;

        public Selectable Host => host;
        public Outline Outline => outline;
        public Image FocusDecoration => focusDecoration;
        public bool IsFocused => outline != null && outline.enabled;

        private void Awake()
        {
            EnsureVisual();
        }

        private void OnEnable()
        {
            EnsureVisual();
        }

        public void SetHost(Selectable value)
        {
            host = value;
            EnsureVisual();
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocused(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetFocused(false);
        }

        public void SetFocused(bool focused)
        {
            EnsureVisual();
            outline.enabled = focused;
            focusDecoration.enabled = focused;
        }

        private void EnsureVisual()
        {
            host = host ?? GetComponent<Selectable>();
            if (outline == null)
            {
                outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
                outline.effectColor = UiFactory.CommandAccent;
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;
                outline.enabled = false;
            }

            if (focusDecoration == null)
            {
                var decorationObject = transform.Find("FocusDecoration");
                if (decorationObject == null)
                {
                    decorationObject = new GameObject("FocusDecoration", typeof(RectTransform), typeof(Image)).transform;
                    decorationObject.SetParent(transform, false);
                }

                focusDecoration = decorationObject.GetComponent<Image>();
                var rect = decorationObject as RectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-2f, -2f);
                rect.offsetMax = new Vector2(2f, 2f);
                rect.SetAsLastSibling();
                focusDecoration.color = new Color(UiFactory.CommandAccent.r, UiFactory.CommandAccent.g, UiFactory.CommandAccent.b, 0.08f);
            }

            focusDecoration.raycastTarget = false;
            focusDecoration.enabled = outline.enabled;
        }
    }
}
