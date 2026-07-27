namespace UnderwaterGliderTwin.Mapping
{
    public readonly struct MissionGeoReference
    {
        public MissionGeoReference(double longitudeDeg, double latitudeDeg)
        {
            LongitudeDeg = longitudeDeg;
            LatitudeDeg = latitudeDeg;
        }

        public double LongitudeDeg { get; }

        public double LatitudeDeg { get; }

        public static bool TryCreate(double longitudeDeg, double latitudeDeg, out MissionGeoReference value, out string error)
        {
            value = default;
            if (double.IsNaN(longitudeDeg) || double.IsInfinity(longitudeDeg) || longitudeDeg < -180d || longitudeDeg > 180d)
            {
                error = "经度必须在 -180 到 180 之间。";
                return false;
            }

            if (double.IsNaN(latitudeDeg) || double.IsInfinity(latitudeDeg) || latitudeDeg < -90d || latitudeDeg > 90d)
            {
                error = "纬度必须在 -90 到 90 之间。";
                return false;
            }

            value = new MissionGeoReference(longitudeDeg, latitudeDeg);
            error = string.Empty;
            return true;
        }
    }
}
