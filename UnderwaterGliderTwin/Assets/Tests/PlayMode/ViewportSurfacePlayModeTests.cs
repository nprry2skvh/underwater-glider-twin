using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ViewportSurfacePlayModeTests
    {
        [UnityTest]
        public IEnumerator ViewportSurface_ResizesAndDoesNotInterceptToolbarRaycasts()
        {
            var canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cameraObject = new GameObject("ViewportCamera", typeof(Camera));
            GameObject eventSystemObject = null;
            try
            {
                if (EventSystem.current == null)
                {
                    eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                }

                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(800f, 600f);

                var uiRoot = UiFactory.EnsureResponsiveRuntimeLayout(canvas);
                var viewportColumn = uiRoot.Find("MainBody/ViewportColumn") as RectTransform;
                viewportColumn.parent.GetComponent<HorizontalLayoutGroup>().enabled = false;
                viewportColumn.anchorMin = viewportColumn.anchorMax = new Vector2(0.5f, 0.5f);
                viewportColumn.sizeDelta = new Vector2(640f, 360f);

                var toolbar = new GameObject("OceanCommandToolbar", typeof(RectTransform)).GetComponent<RectTransform>();
                toolbar.SetParent(viewportColumn, false);
                toolbar.anchorMin = Vector2.zero;
                toolbar.anchorMax = Vector2.one;
                toolbar.offsetMin = Vector2.zero;
                toolbar.offsetMax = Vector2.zero;
                var button = UiFactory.Button("CameraFollowCommand", toolbar, "跟随",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 44f));

                var controller = UiFactory.EnsureViewportSurface(viewportColumn, cameraObject.GetComponent<Camera>());
                var modalRoot = canvas.transform.Find("ModalRoot") as RectTransform;
                var tooltip = UiFactory.EnsureTooltipPopup(modalRoot);
                var focus = UiFactory.EnsureFocusVisual(button);
                focus.SetFocused(true);
                Canvas.ForceUpdateCanvases();
                yield return null;

                var firstTexture = controller.Surface.texture as RenderTexture;
                Assert.That(firstTexture, Is.Not.Null);
                Assert.That(controller.Host.parent, Is.SameAs(viewportColumn));
                Assert.That(toolbar.GetSiblingIndex(), Is.EqualTo(viewportColumn.childCount - 1));
                Assert.That(tooltip.PopupImage.raycastTarget, Is.False);
                Assert.That(tooltip.PopupGroup.blocksRaycasts, Is.False);
                Assert.That(focus.FocusDecoration.raycastTarget, Is.False);

                viewportColumn.sizeDelta = new Vector2(720f, 405f);
                controller.Bind(cameraObject.GetComponent<Camera>(), controller.Host, controller.Surface);
                yield return null;

                var resizedTexture = controller.Surface.texture as RenderTexture;
                Assert.That(resizedTexture, Is.Not.Null);
                Assert.That(resizedTexture, Is.Not.SameAs(firstTexture));
                Assert.That(resizedTexture.width, Is.EqualTo(720));
                Assert.That(resizedTexture.height, Is.EqualTo(405));

                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
                };
                var results = new List<RaycastResult>();
                canvasObject.GetComponent<GraphicRaycaster>().Raycast(pointer, results);

                Assert.That(results.Exists(result => result.gameObject == button.gameObject), Is.True);
                Assert.That(results.Exists(result => result.gameObject == controller.Surface.gameObject), Is.False);
                Assert.That(results.Exists(result => result.gameObject == focus.FocusDecoration.gameObject), Is.False);
            }
            finally
            {
                if (eventSystemObject != null)
                {
                    Object.Destroy(eventSystemObject);
                }
                Object.Destroy(cameraObject);
                Object.Destroy(canvasObject);
            }

            yield return null;
        }
    }
}
