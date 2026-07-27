using System;

namespace UnderwaterGliderTwin.Telemetry
{
    [Serializable]
    public sealed class CopernicusCurrentRequest
    {
        public const string DefaultDatasetId = "cmems_mod_glo_phy-cur_anfc_0.083deg_P1D-m";

        public CopernicusCurrentRequest(double longitudeDeg, double latitudeDeg, float minimumDepthM, float maximumDepthM)
            : this(longitudeDeg, latitudeDeg, minimumDepthM, maximumDepthM, 0f, 0f)
        {
        }

        public CopernicusCurrentRequest(
            double longitudeDeg,
            double latitudeDeg,
            float minimumDepthM,
            float maximumDepthM,
            float prefetchHalfWidthKm,
            float forecastHours)
        {
            this.longitudeDeg = longitudeDeg;
            this.latitudeDeg = latitudeDeg;
            minDepthM = minimumDepthM;
            maxDepthM = maximumDepthM;
            this.prefetchHalfWidthKm = prefetchHalfWidthKm;
            this.forecastHours = forecastHours;
            SetRegionBounds();
            datasetId = DefaultDatasetId;
        }

        public double longitudeDeg;
        public double latitudeDeg;
        public float minDepthM;
        public float maxDepthM;
        public double minimumLongitudeDeg;
        public double maximumLongitudeDeg;
        public double minimumLatitudeDeg;
        public double maximumLatitudeDeg;
        public float prefetchHalfWidthKm;
        public float forecastHours;
        public string datasetId;

        public string DatasetId => string.IsNullOrWhiteSpace(datasetId) ? DefaultDatasetId : datasetId;

        public bool TryValidate(out string error)
        {
            if (double.IsNaN(longitudeDeg) || double.IsInfinity(longitudeDeg))
            {
                error = "Longitude must be a finite number.";
                return false;
            }

            if (double.IsNaN(latitudeDeg) || double.IsInfinity(latitudeDeg) || latitudeDeg < -90d || latitudeDeg > 90d)
            {
                error = "Latitude must be between -90 and 90.";
                return false;
            }

            if (float.IsNaN(minDepthM) || float.IsInfinity(minDepthM) || minDepthM < 0f)
            {
                error = "Minimum depth must be zero or greater.";
                return false;
            }

            if (float.IsNaN(maxDepthM) || float.IsInfinity(maxDepthM) || maxDepthM < minDepthM)
            {
                error = "Maximum depth must not be less than minimum depth.";
                return false;
            }

            if (float.IsNaN(prefetchHalfWidthKm) || float.IsInfinity(prefetchHalfWidthKm) || prefetchHalfWidthKm < 0f || prefetchHalfWidthKm > 250f)
            {
                error = "Prefetch half width must be between 0 and 250 km.";
                return false;
            }

            if (float.IsNaN(forecastHours) || float.IsInfinity(forecastHours) || forecastHours < 0f || forecastHours > 168f)
            {
                error = "Forecast window must be between 0 and 168 hours.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void SetRegionBounds()
        {
            var latitudeHalfSpanDeg = prefetchHalfWidthKm / 111.32d;
            var longitudeScale = 111.32d * Math.Cos(latitudeDeg * Math.PI / 180d);
            var longitudeHalfSpanDeg = Math.Abs(longitudeScale) > 0.001d
                ? prefetchHalfWidthKm / longitudeScale
                : 0d;
            minimumLongitudeDeg = longitudeDeg - longitudeHalfSpanDeg;
            maximumLongitudeDeg = longitudeDeg + longitudeHalfSpanDeg;
            minimumLatitudeDeg = Math.Max(-90d, latitudeDeg - latitudeHalfSpanDeg);
            maximumLatitudeDeg = Math.Min(90d, latitudeDeg + latitudeHalfSpanDeg);
        }
    }
}
