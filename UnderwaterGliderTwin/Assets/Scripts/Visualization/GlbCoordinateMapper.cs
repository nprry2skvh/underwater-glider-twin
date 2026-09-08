using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public static class GlbCoordinateMapper
    {
        public static Vector3 ToGlbPosition(double eastM, double northM, double depthM)
        {
            return new Vector3((float)eastM, (float)-depthM, (float)-northM);
        }
    }
}
