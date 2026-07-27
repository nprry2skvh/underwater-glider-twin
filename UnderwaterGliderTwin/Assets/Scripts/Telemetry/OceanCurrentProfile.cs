using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Telemetry
{
    public sealed class OceanCurrentProfile
    {
        private readonly List<OceanCurrentLayer> layers = new List<OceanCurrentLayer>();

        public OceanCurrentProfile()
        {
        }

        public OceanCurrentProfile(IEnumerable<OceanCurrentLayer> sourceLayers)
        {
            ReplaceLayers(sourceLayers);
        }

        public IReadOnlyList<OceanCurrentLayer> Layers => layers;

        public Vector2 GetVelocity(float depthM)
        {
            OceanCurrentLayer nearestLayer = null;
            var nearestDistance = float.PositiveInfinity;

            foreach (var layer in layers)
            {
                if (layer == null)
                {
                    continue;
                }

                if (layer.Contains(depthM))
                {
                    return new Vector2(layer.EastwardMps, layer.NorthwardMps);
                }

                var distance = layer.DistanceTo(depthM);
                if (distance < nearestDistance)
                {
                    nearestLayer = layer;
                    nearestDistance = distance;
                }
            }

            return nearestLayer == null
                ? Vector2.zero
                : new Vector2(nearestLayer.EastwardMps, nearestLayer.NorthwardMps);
        }

        public bool TryGetInterpolatedVelocity(float depthM, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            var orderedLayers = new List<OceanCurrentLayer>();
            foreach (var layer in layers)
            {
                if (layer != null)
                {
                    orderedLayers.Add(layer);
                }
            }

            if (orderedLayers.Count == 0)
            {
                return false;
            }

            orderedLayers.Sort((left, right) => GetCenterDepth(left).CompareTo(GetCenterDepth(right)));
            for (var index = 1; index < orderedLayers.Count; index++)
            {
                if (GetMinimumDepth(orderedLayers[index]) < GetMaximumDepth(orderedLayers[index - 1]))
                {
                    return false;
                }
            }

            if (depthM <= GetCenterDepth(orderedLayers[0]))
            {
                velocity = GetLayerVelocity(orderedLayers[0]);
                return true;
            }

            for (var index = 1; index < orderedLayers.Count; index++)
            {
                var lower = orderedLayers[index - 1];
                var upper = orderedLayers[index];
                var lowerDepth = GetCenterDepth(lower);
                var upperDepth = GetCenterDepth(upper);
                if (depthM <= upperDepth)
                {
                    var blend = Mathf.InverseLerp(lowerDepth, upperDepth, depthM);
                    velocity = Vector2.Lerp(GetLayerVelocity(lower), GetLayerVelocity(upper), blend);
                    return true;
                }
            }

            velocity = GetLayerVelocity(orderedLayers[orderedLayers.Count - 1]);
            return true;
        }

        public bool TryGetDepthCoverage(out float minimumDepthM, out float maximumDepthM)
        {
            minimumDepthM = float.PositiveInfinity;
            maximumDepthM = float.NegativeInfinity;
            foreach (var layer in layers)
            {
                if (layer == null || float.IsNaN(layer.MinDepthM) || float.IsNaN(layer.MaxDepthM))
                {
                    continue;
                }

                minimumDepthM = Mathf.Min(minimumDepthM, layer.MinDepthM, layer.MaxDepthM);
                maximumDepthM = Mathf.Max(maximumDepthM, layer.MinDepthM, layer.MaxDepthM);
            }

            if (float.IsPositiveInfinity(minimumDepthM) || float.IsNegativeInfinity(maximumDepthM))
            {
                minimumDepthM = 0f;
                maximumDepthM = 0f;
                return false;
            }

            return true;
        }

        public void AddLayer(OceanCurrentLayer layer)
        {
            if (layer != null)
            {
                layers.Add(layer);
            }
        }

        public bool RemoveLayerAt(int index)
        {
            if (index < 0 || index >= layers.Count)
            {
                return false;
            }

            layers.RemoveAt(index);
            return true;
        }

        public void ReplaceLayers(IEnumerable<OceanCurrentLayer> sourceLayers)
        {
            layers.Clear();
            if (sourceLayers == null)
            {
                return;
            }

            foreach (var layer in sourceLayers)
            {
                if (layer != null)
                {
                    layers.Add(layer.Clone());
                }
            }
        }

        public OceanCurrentProfile Clone()
        {
            return new OceanCurrentProfile(layers);
        }

        private static float GetMinimumDepth(OceanCurrentLayer layer)
        {
            return Mathf.Min(layer.MinDepthM, layer.MaxDepthM);
        }

        private static float GetMaximumDepth(OceanCurrentLayer layer)
        {
            return Mathf.Max(layer.MinDepthM, layer.MaxDepthM);
        }

        private static float GetCenterDepth(OceanCurrentLayer layer)
        {
            return (GetMinimumDepth(layer) + GetMaximumDepth(layer)) * 0.5f;
        }

        private static Vector2 GetLayerVelocity(OceanCurrentLayer layer)
        {
            return new Vector2(layer.EastwardMps, layer.NorthwardMps);
        }
    }
}
