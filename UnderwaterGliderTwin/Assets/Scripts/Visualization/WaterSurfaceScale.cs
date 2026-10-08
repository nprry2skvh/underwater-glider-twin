using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public static class WaterSurfaceScale
    {
        public static float DepthToWorld(float depthM, float depthScale)
        {
            return Mathf.Max(0f, depthM) * Mathf.Max(0f, depthScale);
        }
    }
}
