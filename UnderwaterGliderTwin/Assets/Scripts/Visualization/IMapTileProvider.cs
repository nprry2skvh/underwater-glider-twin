namespace UnderwaterGliderTwin.Visualization
{
    public interface IMapTileProvider
    {
        bool TryGetTileUrl(int zoom, int x, int y, MapStyle style, out string url, out string error);
    }
}
