using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public sealed class UiTooltipController : MonoBehaviour
    {
        [SerializeField] private RectTransform tooltipPopup;
        [SerializeField] private Image popupImage;
        [SerializeField] private CanvasGroup popupGroup;
        [SerializeField] private Text messageText;
        [SerializeField, Min(0f)] private float showDelay = 0.35f;
        [SerializeField, Min(80f)] private float maxWidth = 280f;

        private Coroutine pendingShow;
        private UiTooltip source;

        public RectTransform Popup => tooltipPopup;
        public Image PopupImage => popupImage;
        public CanvasGroup PopupGroup => popupGroup;
        public Text MessageText => messageText;
        public bool IsVisible => tooltipPopup != null && tooltipPopup.gameObject.activeSelf && popupGroup != null && popupGroup.alpha > 0.99f;

        private void Awake()
        {
            CachePopupReferences();
            Hide();
        }

        public void ConfigurePopup(RectTransform popup, Image image, CanvasGroup group, Text text)
        {
            tooltipPopup = popup;
            popupImage = image;
            popupGroup = group;
            messageText = text;
            CachePopupReferences();
            ApplyNonBlockingDecorations();
            Hide();
        }

        public void SetShowDelayForTests(float delay)
        {
            showDelay = Mathf.Max(0f, delay);
        }

        public void Show(UiTooltip tooltip)
        {
            if (tooltip == null || string.IsNullOrWhiteSpace(tooltip.Message))
            {
                Hide();
                return;
            }

            CachePopupReferences();
            StopPendingShow();
            source = tooltip;
            if (tooltipPopup != null && !tooltipPopup.gameObject.activeSelf)
            {
                tooltipPopup.gameObject.SetActive(true);
                if (popupGroup != null)
                {
                    popupGroup.alpha = 0f;
                    popupGroup.interactable = false;
                    popupGroup.blocksRaycasts = false;
                }
            }

            if (!isActiveAndEnabled)
            {
                ShowNow();
                return;
            }

            if (showDelay <= 0f)
            {
                ShowNow();
                return;
            }

            pendingShow = StartCoroutine(ShowAfterDelay(tooltip));
        }

        public void Hide()
        {
            StopPendingShow();
            source = null;
            if (popupGroup != null)
            {
                popupGroup.alpha = 0f;
                popupGroup.interactable = false;
                popupGroup.blocksRaycasts = false;
            }

            if (tooltipPopup != null)
            {
                tooltipPopup.gameObject.SetActive(false);
            }
        }

        internal void Hide(UiTooltip expectedSource)
        {
            if (source == expectedSource)
            {
                Hide();
            }
        }

        public void RefreshPosition()
        {
            if (!IsVisible || source == null || tooltipPopup == null)
            {
                return;
            }

            var canvas = tooltipPopup.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            var sourceRect = source.transform as RectTransform;
            if (canvasRect == null || sourceRect == null)
            {
                return;
            }

            var corners = new Vector3[4];
            sourceRect.GetWorldCorners(corners);
            var screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, corners[1]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var localPoint))
            {
                return;
            }

            var popupSize = tooltipPopup.rect.size;
            var canvasRectSize = canvasRect.rect.size;
            var x = Mathf.Clamp(localPoint.x, -canvasRectSize.x * 0.5f + popupSize.x * 0.5f, canvasRectSize.x * 0.5f - popupSize.x * 0.5f);
            var above = localPoint.y + popupSize.y * 0.5f + 8f;
            var below = localPoint.y - popupSize.y * 0.5f - 8f;
            var y = above + popupSize.y * 0.5f <= canvasRectSize.y * 0.5f ? above : below;
            y = Mathf.Clamp(y, -canvasRectSize.y * 0.5f + popupSize.y * 0.5f, canvasRectSize.y * 0.5f - popupSize.y * 0.5f);
            tooltipPopup.anchoredPosition = new Vector2(x, y);
        }

        private IEnumerator ShowAfterDelay(UiTooltip expectedSource)
        {
            yield return new WaitForSecondsRealtime(showDelay);
            pendingShow = null;
            if (source == expectedSource)
            {
                ShowNow();
            }
        }

        private void ShowNow()
        {
            if (source == null || tooltipPopup == null || popupGroup == null || messageText == null)
            {
                Hide();
                return;
            }

            messageText.text = source.Message;
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.verticalOverflow = VerticalWrapMode.Overflow;
            messageText.raycastTarget = false;
            var layout = tooltipPopup.GetComponent<LayoutElement>() ?? tooltipPopup.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = Mathf.Min(maxWidth, Mathf.Max(80f, maxWidth));
            layout.flexibleWidth = 0f;
            ApplyNonBlockingDecorations();
            tooltipPopup.gameObject.SetActive(true);
            popupGroup.alpha = 1f;
            popupGroup.interactable = false;
            popupGroup.blocksRaycasts = false;
            RefreshPosition();
        }

        private void CachePopupReferences()
        {
            if (tooltipPopup == null)
            {
                tooltipPopup = transform as RectTransform;
            }

            if (tooltipPopup == null)
            {
                return;
            }

            popupImage = popupImage ?? tooltipPopup.GetComponent<Image>();
            popupGroup = popupGroup ?? tooltipPopup.GetComponent<CanvasGroup>();
            messageText = messageText ?? tooltipPopup.GetComponentInChildren<Text>(true);
        }

        private void ApplyNonBlockingDecorations()
        {
            if (popupImage != null)
            {
                popupImage.raycastTarget = false;
            }

            if (popupGroup != null)
            {
                popupGroup.interactable = false;
                popupGroup.blocksRaycasts = false;
            }
        }

        private void StopPendingShow()
        {
            if (pendingShow != null)
            {
                StopCoroutine(pendingShow);
                pendingShow = null;
            }
        }
    }
}
