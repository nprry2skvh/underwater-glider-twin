using System;
using UnityEngine;

namespace UnderwaterGliderTwin.Mapping
{
    public static class WebMercatorProjection
    {
        public static Vector2Int LongitudeLatitudeToTile(double longitudeDeg, double latitudeDeg, int zoom)
        {
            var count = 1 << Mathf.Clamp(zoom, 0, 22);
            var latitude = Math.Max(-85.05112878d, Math.Min(85.05112878d, latitudeDeg)) * Math.PI / 180d;
            var x = (int)Math.Floor((longitudeDeg + 180d) / 360d * count);
            var y = (int)Math.Floor((1d - Math.Asinh(Math.Tan(latitude)) / Math.PI) / 2d * count);
            return new Vector2Int(Mathf.Clamp(x, 0, count - 1), Mathf.Clamp(y, 0, count - 1));
        }
    }
}
