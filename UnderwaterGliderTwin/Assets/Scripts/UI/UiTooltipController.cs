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
            var boundaryRect = tooltipPopup.parent as RectTransform ?? canvasRect;
            if (boundaryRect != null && (boundaryRect.rect.width <= 0f || boundaryRect.rect.height <= 0f))
            {
                boundaryRect = canvasRect;
            }
            var sourceRect = source.transform as RectTransform;
            if (canvasRect == null || boundaryRect == null || sourceRect == null)
            {
                return;
            }

            var sourceCorners = new Vector3[4];
            sourceRect.GetWorldCorners(sourceCorners);
            var localCorners = new Vector2[4];
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            for (var i = 0; i < sourceCorners.Length; i++)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, sourceCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boundaryRect, screenPoint, camera, out localCorners[i]))
                {
                    return;
                }
            }

            var sourceMinX = localCorners[0].x;
            var sourceMaxX = localCorners[0].x;
            var sourceMinY = localCorners[0].y;
            var sourceMaxY = localCorners[0].y;
            for (var i = 1; i < localCorners.Length; i++)
            {
                sourceMinX = Mathf.Min(sourceMinX, localCorners[i].x);
                sourceMaxX = Mathf.Max(sourceMaxX, localCorners[i].x);
                sourceMinY = Mathf.Min(sourceMinY, localCorners[i].y);
                sourceMaxY = Mathf.Max(sourceMaxY, localCorners[i].y);
            }

            var popupSize = tooltipPopup.rect.size;
            const float gap = 8f;
            var popupHalfWidth = popupSize.x * 0.5f;
            var popupHalfHeight = popupSize.y * 0.5f;
            var boundaryMinX = boundaryRect.rect.xMin;
            var boundaryMaxX = boundaryRect.rect.xMax;
            var boundaryMinY = boundaryRect.rect.yMin;
            var boundaryMaxY = boundaryRect.rect.yMax;
            var sourceCenterX = (sourceMinX + sourceMaxX) * 0.5f;
            var sourceCenterY = (sourceMinY + sourceMaxY) * 0.5f;
            var x = Mathf.Clamp(sourceCenterX, boundaryMinX + popupHalfWidth, boundaryMaxX - popupHalfWidth);
            var above = sourceMaxY + gap + popupHalfHeight;
            var below = sourceMinY - gap - popupHalfHeight;
            float y;
            if (above + popupHalfHeight <= boundaryMaxY)
            {
                y = above;
            }
            else if (below - popupHalfHeight >= boundaryMinY)
            {
                y = below;
            }
            else
            {
                y = Mathf.Clamp(sourceCenterY,
                    boundaryMinY + popupHalfHeight,
                    boundaryMaxY - popupHalfHeight);
            }

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
