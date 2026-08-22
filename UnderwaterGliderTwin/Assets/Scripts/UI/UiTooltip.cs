using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public sealed class UiTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField, TextArea(1, 4)] private string message;
        [SerializeField] private Selectable host;
        [SerializeField] private UiTooltipController controller;

        public string Message => message;
        public Selectable Host => host;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void SetMessage(string value)
        {
            message = value ?? string.Empty;
        }

        public void SetHost(Selectable value)
        {
            host = value;
        }

        public void SetController(UiTooltipController value)
        {
            controller = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ResolveReferences();
            controller?.Show(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            controller?.Hide(this);
        }

        public void OnSelect(BaseEventData eventData)
        {
            ResolveReferences();
            controller?.Show(this);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            controller?.Hide(this);
        }

        private void OnDisable()
        {
            controller?.Hide(this);
        }

        private void ResolveReferences()
        {
            if (host == null)
            {
                host = GetComponent<Selectable>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<UiTooltipController>(true);
            }

            if (controller == null)
            {
                controller = FindObjectOfType<UiTooltipController>(true);
            }
        }
    }
}
