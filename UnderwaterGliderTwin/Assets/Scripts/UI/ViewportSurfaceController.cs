using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterGliderTwin.UI
{
    public sealed class ViewportSurfaceController : MonoBehaviour
    {
        private Camera sourceCamera;
        private RectTransform host;
        private RawImage surface;
        private Canvas hostCanvas;
        private RenderTexture ownedTexture;
        private RenderTexture previousCameraTarget;
        private Texture previousSurfaceTexture;
        private Canvas.WillRenderCanvases canvasRenderCallback;
        private bool subscribedToCanvasRender;

        public RectTransform Host => host;
        public RawImage Surface => surface;
        public RenderTexture OwnedTexture => ownedTexture;

        public void Bind(Camera boundCamera, RectTransform boundHost, RawImage boundSurface)
        {
            if (sourceCamera != boundCamera || host != boundHost || surface != boundSurface)
            {
                ReleaseOwnedTexture(true);
                sourceCamera = boundCamera;
                host = boundHost;
                surface = boundSurface;
                hostCanvas = host != null ? host.GetComponentInParent<Canvas>() : null;
            }

            ConfigureSurface();
            if (isActiveAndEnabled)
            {
                SubscribeToCanvasRender();
                RefreshForCurrentSize();
            }
        }

        public void RefreshForCurrentSize()
        {
            if (this == null)
            {
                UnsubscribeFromCanvasRender();
                return;
            }

            if (!isActiveAndEnabled || sourceCamera == null || host == null || surface == null)
            {
                return;
            }

            var scaleFactor = hostCanvas != null ? Mathf.Max(0.01f, hostCanvas.scaleFactor) : 1f;
            var width = Mathf.Max(1, Mathf.RoundToInt(host.rect.width * scaleFactor));
            var height = Mathf.Max(1, Mathf.RoundToInt(host.rect.height * scaleFactor));
            var maximumTextureSize = Mathf.Max(1, SystemInfo.maxTextureSize);
            width = Mathf.Min(width, maximumTextureSize);
            height = Mathf.Min(height, maximumTextureSize);

            if (ownedTexture != null && ownedTexture.width == width && ownedTexture.height == height)
            {
                ApplyOwnedTexture();
                return;
            }

            if (ownedTexture == null)
            {
                CapturePreviousTargets();
            }

            ReplaceOwnedTexture(width, height);
        }

        private void CapturePreviousTargets()
        {
            previousCameraTarget = sourceCamera != null ? sourceCamera.targetTexture : null;
            previousSurfaceTexture = surface != null ? surface.texture : null;
        }

        private void OnEnable()
        {
            SubscribeToCanvasRender();
            RefreshForCurrentSize();
        }

        private void OnDisable()
        {
            UnsubscribeFromCanvasRender();
            ReleaseOwnedTexture(true);
        }

        private void OnDestroy()
        {
            UnsubscribeFromCanvasRender();
            ReleaseOwnedTexture(true);
            sourceCamera = null;
            host = null;
            surface = null;
            hostCanvas = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshForCurrentSize();
        }

        private void ConfigureSurface()
        {
            if (host == null || surface == null)
            {
                return;
            }

            if (surface.transform.parent != host)
            {
                surface.rectTransform.SetParent(host, false);
            }

            var rect = surface.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            surface.raycastTarget = false;
            surface.color = Color.white;
            surface.transform.SetAsFirstSibling();
        }

        private void ReplaceOwnedTexture(int width, int height)
        {
            ReleaseOwnedTexture(false);
            ownedTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "ViewportSurfaceRenderTexture",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            ownedTexture.Create();
            ApplyOwnedTexture();
        }

        private void ApplyOwnedTexture()
        {
            if (sourceCamera != null)
            {
                sourceCamera.targetTexture = ownedTexture;
            }

            if (surface != null)
            {
                surface.texture = ownedTexture;
            }
        }

        private void ReleaseOwnedTexture(bool restorePreviousTargets)
        {
            var texture = ownedTexture;
            if (texture == null)
            {
                return;
            }

            if (restorePreviousTargets)
            {
                RestorePreviousTargets(texture);
            }
            else
            {
                if (sourceCamera != null && sourceCamera.targetTexture == texture)
                {
                    sourceCamera.targetTexture = null;
                }

                if (surface != null && surface.texture == texture)
                {
                    surface.texture = null;
                }
            }

            ownedTexture = null;
            texture.Release();
            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        private void RestorePreviousTargets(RenderTexture expectedTexture)
        {
            if (expectedTexture == null)
            {
                return;
            }

            if (sourceCamera != null && sourceCamera.targetTexture == expectedTexture)
            {
                sourceCamera.targetTexture = previousCameraTarget;
            }

            if (surface != null && surface.texture == expectedTexture)
            {
                surface.texture = previousSurfaceTexture;
            }
        }

        private void SubscribeToCanvasRender()
        {
            if (subscribedToCanvasRender)
            {
                return;
            }

            if (canvasRenderCallback == null)
            {
                canvasRenderCallback = RefreshForCurrentSize;
            }

            Canvas.willRenderCanvases += canvasRenderCallback;
            subscribedToCanvasRender = true;
        }

        private void UnsubscribeFromCanvasRender()
        {
            if (!subscribedToCanvasRender)
            {
                return;
            }

            Canvas.willRenderCanvases -= canvasRenderCallback;
            subscribedToCanvasRender = false;
        }
    }
}
