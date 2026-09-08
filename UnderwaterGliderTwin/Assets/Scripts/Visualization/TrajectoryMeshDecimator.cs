using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderwaterGliderTwin.Visualization
{
    public static class TrajectoryMeshDecimator
    {
        public static IReadOnlyList<Vector3> Decimate(IReadOnlyList<Vector3> source, int budget, IReadOnlyCollection<int> requiredBoundaryIndices)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Count <= Math.Max(2, budget)) return new List<Vector3>(source);
            budget = Math.Max(2, budget);
            var required = new SortedSet<int>(requiredBoundaryIndices ?? Array.Empty<int>());
            required.Add(0); required.Add(source.Count - 1);
            var indices = new SortedSet<int>(required);
            for (var i = 0; i < budget && indices.Count < budget; i++)
            {
                var index = (int)Math.Round(i * (source.Count - 1d) / Math.Max(1, budget - 1));
                indices.Add(index);
            }
            var result = new List<Vector3>(indices.Count);
            foreach (var index in indices) if (index >= 0 && index < source.Count) result.Add(source[index]);
            return result;
        }
    }
}
