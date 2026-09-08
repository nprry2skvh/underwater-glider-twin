using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public enum OceanCurrentDataKind
    {
        None,
        LayeredUniform,
        NetworkGrid
    }

    public readonly struct OceanCurrentQuery
    {
        public OceanCurrentQuery(double longitudeDeg, double latitudeDeg, Vector3 enuPositionM, float depthM, float elapsedSeconds)
        {
            LongitudeDeg = longitudeDeg;
            LatitudeDeg = latitudeDeg;
            EnuPositionM = enuPositionM;
            DepthM = depthM;
            ElapsedSeconds = elapsedSeconds;
        }

        public double LongitudeDeg { get; }
        public double LatitudeDeg { get; }
        public Vector3 EnuPositionM { get; }
        public float DepthM { get; }
        public float ElapsedSeconds { get; }
    }

    public readonly struct OceanCurrentVector
    {
        public OceanCurrentVector(Vector3 velocityMps, OceanCurrentDataKind dataKind)
        {
            VelocityMps = velocityMps;
            DataKind = dataKind;
        }

        public Vector3 VelocityMps { get; }
        public OceanCurrentDataKind DataKind { get; }
    }

    public sealed class OceanCurrentResolver
    {
        private readonly SimulationProfile profile;

        public OceanCurrentResolver(SimulationProfile profile)
        {
            this.profile = profile ?? SimulationProfile.Default;
        }

        public bool TryGetVector(OceanCurrentQuery query, out OceanCurrentVector vector)
        {
            if (profile.OceanCurrentSourcePreference == OceanCurrentSourcePreference.NetworkPreferred)
            {
                return TryGetNetwork(query, out vector) || TryGetLayered(query, out vector);
            }

            return TryGetLayered(query, out vector) || TryGetNetwork(query, out vector);
        }

        private bool TryGetLayered(OceanCurrentQuery query, out OceanCurrentVector vector)
        {
            vector = default;
            var currentProfile = profile.OceanCurrentProfile;
            if (currentProfile == null || currentProfile.Layers.Count == 0)
            {
                return false;
            }

            if (!currentProfile.TryGetInterpolatedVelocity(query.DepthM, out var velocity))
            {
                return false;
            }

            vector = new OceanCurrentVector(new Vector3(velocity.x, 0f, velocity.y), OceanCurrentDataKind.LayeredUniform);
            return true;
        }

        private bool TryGetNetwork(OceanCurrentQuery query, out OceanCurrentVector vector)
        {
            vector = default;
            if (profile.OceanCurrentField == null
                || !profile.OceanCurrentField.TryGetVector(
                    query.LongitudeDeg,
                    query.LatitudeDeg,
                    query.DepthM,
                    query.ElapsedSeconds,
                    profile.IrregularFieldIdwRadiusKm,
                    out var velocity))
            {
                return false;
            }

            vector = new OceanCurrentVector(velocity, OceanCurrentDataKind.NetworkGrid);
            return true;
        }
    }
}
