namespace UnderwaterGliderTwin.Visualization
{
    public sealed class OpenMapTilesClient : IMapTileProvider
    {
        public bool TryGetTileUrl(int zoom, int x, int y, MapStyle style, out string url, out string error)
        {
            url = $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
            error = string.Empty;
            return true;
        }
    }
}
