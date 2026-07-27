using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class OceanCurrentLayer
    {
        public OceanCurrentLayer()
        {
        }

        public OceanCurrentLayer(float minDepthM, float maxDepthM, float eastwardMps, float northwardMps)
        {
            MinDepthM = minDepthM;
            MaxDepthM = maxDepthM;
            EastwardMps = eastwardMps;
            NorthwardMps = northwardMps;
        }

        public float MinDepthM { get; set; }

        public float MaxDepthM { get; set; }

        public float EastwardMps { get; set; }

        public float NorthwardMps { get; set; }

        public bool Contains(float depthM)
        {
            var minimum = Mathf.Min(MinDepthM, MaxDepthM);
            var maximum = Mathf.Max(MinDepthM, MaxDepthM);
            return depthM >= minimum && depthM <= maximum;
        }

        public float DistanceTo(float depthM)
        {
            var minimum = Mathf.Min(MinDepthM, MaxDepthM);
            var maximum = Mathf.Max(MinDepthM, MaxDepthM);
            if (depthM < minimum)
            {
                return minimum - depthM;
            }

            return depthM > maximum ? depthM - maximum : 0f;
        }

        public OceanCurrentLayer Clone()
        {
            return new OceanCurrentLayer(MinDepthM, MaxDepthM, EastwardMps, NorthwardMps);
        }
    }
}
