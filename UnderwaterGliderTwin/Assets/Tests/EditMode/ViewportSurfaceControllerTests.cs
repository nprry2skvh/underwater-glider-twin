using System.Collections.Generic;
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
        private readonly List<GameObject> auxiliaryObjects = new List<GameObject>();
        private readonly List<RenderTexture> externalTextures = new List<RenderTexture>();

        [TearDown]
        public void TearDown()
        {
            for (var index = auxiliaryObjects.Count - 1; index >= 0; index--)
            {
                if (auxiliaryObjects[index] != null)
                {
                    Object.DestroyImmediate(auxiliaryObjects[index]);
                }
            }
            auxiliaryObjects.Clear();

            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
            }

            if (canvasObject != null)
            {
                Object.DestroyImmediate(canvasObject);
            }

            for (var index = externalTextures.Count - 1; index >= 0; index--)
            {
                if (externalTextures[index] == null)
                {
                    continue;
                }

                externalTextures[index].Release();
                Object.DestroyImmediate(externalTextures[index]);
            }
            externalTextures.Clear();
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
        public void Disable_RestoresPreExistingTargets_WhenControllerStillOwnsAssignments()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            var previousCameraTarget = CreateExternalTexture("PreviousCameraTarget");
            var previousSurfaceTexture = CreateExternalTexture("PreviousSurfaceTexture");
            camera.targetTexture = previousCameraTarget;
            surface.texture = previousSurfaceTexture;
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();

            controller.Bind(camera, host, surface);
            Assert.That(camera.targetTexture, Is.SameAs(controller.OwnedTexture));
            Assert.That(surface.texture, Is.SameAs(controller.OwnedTexture));

            controller.enabled = false;

            Assert.That(camera.targetTexture, Is.SameAs(previousCameraTarget));
            Assert.That(surface.texture, Is.SameAs(previousSurfaceTexture));
        }

        [Test]
        public void Reenable_CapturesTargetsInstalledBetweenOwnershipCycles()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            var firstCameraTarget = CreateExternalTexture("FirstCameraTarget");
            var firstSurfaceTexture = CreateExternalTexture("FirstSurfaceTexture");
            camera.targetTexture = firstCameraTarget;
            surface.texture = firstSurfaceTexture;
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();

            controller.Bind(camera, host, surface);
            controller.enabled = false;
            Assert.That(camera.targetTexture, Is.SameAs(firstCameraTarget));
            Assert.That(surface.texture, Is.SameAs(firstSurfaceTexture));

            var secondCameraTarget = CreateExternalTexture("SecondCameraTarget");
            var secondSurfaceTexture = CreateExternalTexture("SecondSurfaceTexture");
            camera.targetTexture = secondCameraTarget;
            surface.texture = secondSurfaceTexture;

            controller.enabled = true;
            Assert.That(camera.targetTexture, Is.SameAs(controller.OwnedTexture));
            Assert.That(surface.texture, Is.SameAs(controller.OwnedTexture));

            controller.enabled = false;

            Assert.That(camera.targetTexture, Is.SameAs(secondCameraTarget));
            Assert.That(surface.texture, Is.SameAs(secondSurfaceTexture));
        }

        [Test]
        public void DestroyWhileActive_RestoresTargetsCapturedForCurrentOwnershipCycle()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            var previousCameraTarget = CreateExternalTexture("PreviousCameraTarget");
            var previousSurfaceTexture = CreateExternalTexture("PreviousSurfaceTexture");
            camera.targetTexture = previousCameraTarget;
            surface.texture = previousSurfaceTexture;
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();
            controller.Bind(camera, host, surface);

            Object.DestroyImmediate(controller);

            Assert.That(camera.targetTexture, Is.SameAs(previousCameraTarget));
            Assert.That(surface.texture, Is.SameAs(previousSurfaceTexture));
        }

        [Test]
        public void RebindWhileActive_RestoresEachBindingsOwnPreExistingTargets()
        {
            var canvas = CreateCanvas();
            var firstHost = CreateRect("FirstViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var firstSurface = CreateSurface(firstHost);
            var firstCamera = CreateCamera();
            var firstCameraTarget = CreateExternalTexture("FirstCameraTarget");
            var firstSurfaceTexture = CreateExternalTexture("FirstSurfaceTexture");
            firstCamera.targetTexture = firstCameraTarget;
            firstSurface.texture = firstSurfaceTexture;

            var secondHost = CreateRect("SecondViewportSurfaceHost", canvas.transform, new Vector2(320f, 180f));
            var secondSurface = CreateSurface(secondHost);
            var secondCamera = CreateAuxiliaryCamera("SecondViewportCamera");
            var secondCameraTarget = CreateExternalTexture("SecondCameraTarget");
            var secondSurfaceTexture = CreateExternalTexture("SecondSurfaceTexture");
            secondCamera.targetTexture = secondCameraTarget;
            secondSurface.texture = secondSurfaceTexture;

            var controller = firstHost.gameObject.AddComponent<ViewportSurfaceController>();
            controller.Bind(firstCamera, firstHost, firstSurface);
            controller.Bind(secondCamera, secondHost, secondSurface);

            Assert.That(firstCamera.targetTexture, Is.SameAs(firstCameraTarget));
            Assert.That(firstSurface.texture, Is.SameAs(firstSurfaceTexture));
            Assert.That(secondCamera.targetTexture, Is.SameAs(controller.OwnedTexture));
            Assert.That(secondSurface.texture, Is.SameAs(controller.OwnedTexture));

            Object.DestroyImmediate(controller);

            Assert.That(secondCamera.targetTexture, Is.SameAs(secondCameraTarget));
            Assert.That(secondSurface.texture, Is.SameAs(secondSurfaceTexture));
        }

        [Test]
        public void ResizeWithinOwnershipCycle_DoesNotOverwriteInitialTargetSnapshots()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            var previousCameraTarget = CreateExternalTexture("PreviousCameraTarget");
            var previousSurfaceTexture = CreateExternalTexture("PreviousSurfaceTexture");
            camera.targetTexture = previousCameraTarget;
            surface.texture = previousSurfaceTexture;
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();

            controller.Bind(camera, host, surface);
            controller.Bind(camera, host, surface);
            host.sizeDelta = new Vector2(512f, 288f);
            controller.RefreshForCurrentSize();
            Assert.That(camera.targetTexture.width, Is.EqualTo(512));
            Assert.That(camera.targetTexture.height, Is.EqualTo(288));

            controller.enabled = false;

            Assert.That(camera.targetTexture, Is.SameAs(previousCameraTarget));
            Assert.That(surface.texture, Is.SameAs(previousSurfaceTexture));
        }

        [Test]
        public void Disable_PreservesExternalTargets_WhenControllerNoLongerOwnsAssignments()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            camera.targetTexture = CreateExternalTexture("PreviousCameraTarget");
            surface.texture = CreateExternalTexture("PreviousSurfaceTexture");
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();
            controller.Bind(camera, host, surface);
            var externalCameraTarget = CreateExternalTexture("ExternalCameraTarget");
            var externalSurfaceTexture = CreateExternalTexture("ExternalSurfaceTexture");
            camera.targetTexture = externalCameraTarget;
            surface.texture = externalSurfaceTexture;

            controller.enabled = false;

            Assert.That(camera.targetTexture, Is.SameAs(externalCameraTarget));
            Assert.That(surface.texture, Is.SameAs(externalSurfaceTexture));
        }

        [Test]
        public void DestroyAfterDisable_DoesNotRestoreStaleTargets_WhenControllerOwnsNoTexture()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            camera.targetTexture = CreateExternalTexture("PreviousCameraTarget");
            surface.texture = CreateExternalTexture("PreviousSurfaceTexture");
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();
            controller.Bind(camera, host, surface);
            controller.enabled = false;
            var externalCameraTarget = CreateExternalTexture("ExternalCameraTarget");
            var externalSurfaceTexture = CreateExternalTexture("ExternalSurfaceTexture");
            camera.targetTexture = externalCameraTarget;
            surface.texture = externalSurfaceTexture;

            Object.DestroyImmediate(controller);

            Assert.That(camera.targetTexture, Is.SameAs(externalCameraTarget));
            Assert.That(surface.texture, Is.SameAs(externalSurfaceTexture));
        }

        [Test]
        public void RebindAfterDisable_DoesNotRestoreStaleTargets_WhenControllerOwnsNoTexture()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, new Vector2(256f, 144f));
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            camera.targetTexture = CreateExternalTexture("PreviousCameraTarget");
            surface.texture = CreateExternalTexture("PreviousSurfaceTexture");
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();
            controller.Bind(camera, host, surface);
            controller.enabled = false;
            var externalCameraTarget = CreateExternalTexture("ExternalCameraTarget");
            var externalSurfaceTexture = CreateExternalTexture("ExternalSurfaceTexture");
            camera.targetTexture = externalCameraTarget;
            surface.texture = externalSurfaceTexture;

            var replacementHost = CreateRect("ReplacementViewportSurfaceHost", canvas.transform, new Vector2(320f, 180f));
            var replacementSurface = CreateSurface(replacementHost);
            var replacementCamera = CreateAuxiliaryCamera("ReplacementViewportCamera");
            var replacementCameraTarget = CreateExternalTexture("ReplacementCameraTarget");
            var replacementSurfaceTexture = CreateExternalTexture("ReplacementSurfaceTexture");
            replacementCamera.targetTexture = replacementCameraTarget;
            replacementSurface.texture = replacementSurfaceTexture;
            controller.Bind(replacementCamera, replacementHost, replacementSurface);

            Assert.That(camera.targetTexture, Is.SameAs(externalCameraTarget));
            Assert.That(surface.texture, Is.SameAs(externalSurfaceTexture));
            Assert.That(replacementCamera.targetTexture, Is.SameAs(replacementCameraTarget));
            Assert.That(replacementSurface.texture, Is.SameAs(replacementSurfaceTexture));
        }

        [Test]
        public void ZeroSizeAndNullBinding_UseSafeMinimumAndRestoreOwnedTargetsOnce()
        {
            var canvas = CreateCanvas();
            var host = CreateRect("ViewportSurfaceHost", canvas.transform, Vector2.zero);
            var surface = CreateSurface(host);
            var camera = CreateCamera();
            var previousCameraTarget = CreateExternalTexture("PreviousCameraTarget");
            var previousSurfaceTexture = CreateExternalTexture("PreviousSurfaceTexture");
            camera.targetTexture = previousCameraTarget;
            surface.texture = previousSurfaceTexture;
            var controller = host.gameObject.AddComponent<ViewportSurfaceController>();

            Assert.DoesNotThrow(() => controller.Bind(camera, host, surface));
            Assert.That(controller.OwnedTexture.width, Is.EqualTo(1));
            Assert.That(controller.OwnedTexture.height, Is.EqualTo(1));

            Assert.DoesNotThrow(() => controller.Bind(null, null, null));
            Assert.That(camera.targetTexture, Is.SameAs(previousCameraTarget));
            Assert.That(surface.texture, Is.SameAs(previousSurfaceTexture));
            Assert.DoesNotThrow(() => controller.Bind(null, null, null));
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

        private Camera CreateAuxiliaryCamera(string name)
        {
            var created = new GameObject(name, typeof(Camera));
            auxiliaryObjects.Add(created);
            return created.GetComponent<Camera>();
        }

        private RenderTexture CreateExternalTexture(string name)
        {
            var texture = new RenderTexture(8, 8, 0) { name = name };
            externalTextures.Add(texture);
            return texture;
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
