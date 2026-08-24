using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnderwaterGliderTwin.UI;

namespace UnderwaterGliderTwin.Tests
{
    public sealed class ViewportSurfaceControllerTests
    {
        private GameObject canvasObject;
        private GameObject cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }

            if (canvasObject != null)
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void Bind_ReusesTextureUntilViewportPixelSizeChanges()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(320f, 180f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();

            controller.Bind(camera, host, surface);
            var firstTexture = camera.targetTexture;

            Assert.That(firstTexture, Is.Not.Null);
            Assert.That(surface.texture, Is.SameAs(firstTexture));
            Assert.That(firstTexture.width, Is.EqualTo(320));
            Assert.That(firstTexture.height, Is.EqualTo(180));

            controller.Bind(camera, host, surface);
            Assert.That(camera.targetTexture, Is.SameAs(firstTexture), "Idempotent binding must not allocate a replacement texture.");

            host.sizeDelta = new Vector2(640f, 360f);
            controller.Bind(camera, host, surface);

            Assert.That(camera.targetTexture, Is.Not.SameAs(firstTexture));
            Assert.That(surface.texture, Is.SameAs(camera.targetTexture));
            Assert.That(camera.targetTexture.width, Is.EqualTo(640));
            Assert.That(camera.targetTexture.height, Is.EqualTo(360));
        }

        [Test]
        public void Disable_ReleasesOwnedTexture_AndEnableRestoresRendering()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();

            controller.Bind(camera, host, surface);
            Assert.That(camera.targetTexture, Is.Not.Null);

            host.gameObject.SetActive(false);
            Assert.That(camera.targetTexture, Is.Null);
            Assert.That(surface.texture, Is.Null);

            host.gameObject.SetActive(true);
            Assert.That(camera.targetTexture, Is.Not.Null);
            Assert.That(surface.texture, Is.SameAs(camera.targetTexture));
        }

        [Test]
        public void EnsureViewportSurface_KeepsOneCanvasOneModalRootAndToolbarAboveSurface()
        {
            var canvas = CreateCanvas();
            var uiRoot = UiFactory.EnsureResponsiveRuntimeLayout(canvas);
            var viewportColumn = uiRoot.Find("MainBody/ViewportColumn") as RectTransform;
            var toolbar = CreateRect("OceanCommandToolbar", viewportColumn, new Vector2(640f, 360f));
            var camera = CreateCamera();

            var controller = UiFactory.EnsureViewportSurface(viewportColumn, camera);

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Host.parent, Is.SameAs(viewportColumn));
            Assert.That(controller.Surface.transform.parent, Is.SameAs(controller.Host));
            Assert.That(controller.Host.GetSiblingIndex(), Is.LessThan(toolbar.GetSiblingIndex()));
            Assert.That(toolbar.GetSiblingIndex(), Is.EqualTo(viewportColumn.childCount - 1));
            Assert.That(controller.Surface.raycastTarget, Is.False);
            Assert.That(canvas.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
            Assert.That(CountChildrenNamed(canvas.transform, "ModalRoot"), Is.EqualTo(1));
        }

        private Canvas CreateCanvas()
        {
            canvasObject = new GameObject("RuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            return canvas;
        }

        private Camera CreateCamera()
        {
            cameraObject = new GameObject("ViewportCamera", typeof(Camera));
            return cameraObject.GetComponent<Camera>();
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static RawImage CreateSurface(RectTransform host)
        {
            var surface = new GameObject("ViewportSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            surface.rectTransform.SetParent(host, false);
            return surface;
        }

        private static int CountChildrenNamed(Transform root, string name)
        {
            var count = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
