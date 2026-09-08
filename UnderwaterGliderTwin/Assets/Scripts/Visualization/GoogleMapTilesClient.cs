using System;

namespace UnderwaterGliderTwin.Visualization
{
    public sealed class GoogleMapTilesClient : IMapTileProvider
    {
        private readonly MapProviderSettings settings;

        public GoogleMapTilesClient(MapProviderSettings settings)
        {
            this.settings = settings ?? new MapProviderSettings();
        }

        public bool TryGetTileUrl(int zoom, int x, int y, MapStyle style, out string url, out string error)
        {
            url = string.Empty;
            if (string.IsNullOrWhiteSpace(settings.GoogleApiKey))
            {
                error = "未配置 Google API Key，已使用开发地图。";
                return false;
            }

            error = "Google Map Tiles 需要会话令牌，当前已保留本机配置入口。";
            return false;
        }
    }
}
