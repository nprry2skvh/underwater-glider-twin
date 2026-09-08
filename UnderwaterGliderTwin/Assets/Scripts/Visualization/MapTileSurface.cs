using System.Collections;
using System.IO;
using UnderwaterGliderTwin.Mapping;
using UnityEngine;
using UnityEngine.Networking;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class MapTileSurface : MonoBehaviour
    {
        private const int DefaultZoom = 8;

        public void Initialize(double longitudeDeg, double latitudeDeg, float width = 38f)
        {
            var tile = WebMercatorProjection.LongitudeLatitudeToTile(longitudeDeg, latitudeDeg, DefaultZoom);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "任务区域地图底图";
            surface.transform.SetParent(transform, false);
            surface.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            var scale = Mathf.Max(20f, width) / 10f;
            surface.transform.localScale = new Vector3(scale, 1f, scale);
            var renderer = surface.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterialFactory.Transparent("任务区域地图底图材质", new Color(0.25f, 0.62f, 0.7f, 0.46f));
            var settings = MapProviderSettings.Load();
            IMapTileProvider provider = settings.Mode == MapProviderMode.Google
                ? new GoogleMapTilesClient(settings)
                : new OpenMapTilesClient();
            if (!provider.TryGetTileUrl(DefaultZoom, tile.x, tile.y, settings.Style, out var url, out _))
            {
                provider = new OpenMapTilesClient();
                provider.TryGetTileUrl(DefaultZoom, tile.x, tile.y, MapStyle.Street, out url, out _);
            }
            if (!string.IsNullOrEmpty(url))
            {
                StartCoroutine(LoadTile(renderer, url, DefaultZoom, tile.x, tile.y));
            }
        }

        private static IEnumerator LoadTile(Renderer renderer, string url, int zoom, int x, int y)
        {
            var cachePath = Path.Combine(Application.persistentDataPath, "MapCache", $"{zoom}-{x}-{y}.png");
            if (File.Exists(cachePath))
            {
                ApplyTexture(renderer, File.ReadAllBytes(cachePath));
                yield break;
            }

            using var request = UnityWebRequestTexture.GetTexture(url, nonReadable: true);
            request.SetRequestHeader("User-Agent", "UnderwaterGliderTwin/1.0");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success || renderer == null)
            {
                yield break;
            }

            var bytes = request.downloadHandler.data;
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
            File.WriteAllBytes(cachePath, bytes);
            ApplyTexture(renderer, bytes);
        }

        private static void ApplyTexture(Renderer renderer, byte[] bytes)
        {
            if (renderer == null || bytes == null || bytes.Length == 0) return;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, markNonReadable: true)) return;
            renderer.sharedMaterial.mainTexture = texture;
            renderer.sharedMaterial.color = new Color(1f, 1f, 1f, 0.46f);
        }
    }
}
